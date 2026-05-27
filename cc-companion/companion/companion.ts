#!/usr/bin/env tsx
/**
 * Claude Code — RimWorldMCP 游戏 AI 助手
 *
 * 用法: tsx companion.ts [--port 19999] [--token xxx] [--model sonnet]
 * 详见 README.md
 */

import { writeFileSync, unlinkSync, existsSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { CONFIG, parseArgs } from './config.js';
import { loadClaudeSdk } from './sdk-loader.js';
import { createWSServer } from '../bridge/ws-server.js';
import { createSession, createResponseProcessor } from '../bridge/session.js';
import { getChatPageHtml } from '../chat/chat-page.js';
import { setupChatHttp } from '../chat/chat-http.js';
import { MessageBus } from '../bridge/message-bus.js';

parseArgs(process.argv);

async function main(): Promise<void> {
  console.log('='.repeat(60));
  console.log('Claude Code — RimWorldMCP 游戏 AI 助手');
  console.log('='.repeat(60));
  console.log(`CWD: ${process.cwd()}`);
  console.log(`ARGV: ${process.argv.slice(2).join(' ')}`);

  console.log(`配置: host=${CONFIG.host} port=${CONFIG.port}`);
  console.log(`模型: ${CONFIG.modelName}`);
  console.log(`数据目录: ${CONFIG.projectPath}`);
  console.log('');

  // 1. 写出 project/local setting sources（C# 传入的 MCP 权限模版等）
  if (CONFIG.projectSettingSources) {
    writeSettingFile(join(CONFIG.projectPath, '.claude', 'settings.json'), CONFIG.projectSettingSources);
  }
  if (CONFIG.localSettingSources) {
    writeSettingFile(join(CONFIG.projectPath, '.claude', 'settings.local.json'), CONFIG.localSettingSources);
  }

  // 2. 生成聊天页面 HTML
  const chatPageHtml = CONFIG.chatPageEnabled ? getChatPageHtml({
    token: CONFIG.token,
    modelName: CONFIG.modelName,
    projectPath: CONFIG.projectPath,
  }) : '';

  // 3. 加载 SDK
  console.log('[cc-companion] 加载 Claude Agent SDK...');
  let sdk: any;
  try {
    sdk = await loadClaudeSdk();
  } catch (err: any) {
    console.error(`[cc-companion] SDK 加载失败: ${err.message}`);
    process.exit(1);
  }

  // 4. 启动 WebSocket 服务器（先于 session，broadcast 需要引用）
  console.log('[cc-companion] 启动 WebSocket 服务器...');

  let lastInitSnapshot = '';
  const onInit = (msg: any) => {
    const parts: string[] = [];
    parts.push('模型=' + (msg.model || '?'));
    parts.push('版本=' + (msg.claude_code_version || '?'));
    parts.push('Key=' + (msg.apiKeySource || '?'));
    parts.push('权限=' + (msg.permissionMode || '?'));
    parts.push('会话=' + (msg.session_id || '?'));
    if (msg.tools?.length) parts.push('工具=' + msg.tools.length);
    if (msg.mcp_servers?.length) parts.push('MCP=' + msg.mcp_servers.map((s: any) => s.name).join(','));
    const snapshot = parts.join(' | ');

    // 首次或变更时才输出日志 + 推送 bus
    if (!lastInitSnapshot || snapshot !== lastInitSnapshot) {
      lastInitSnapshot = snapshot;
      console.log('[cc-companion] SDK 初始化完成 | ' + snapshot);
      if (msg.model) {
        CONFIG.resolvedModel = msg.model;
        bus?.publishModelInfo(msg.model);
      }
    }
  };

  let abortController = new AbortController();
  let { inputStream, queryIterator } = createSession(sdk, CONFIG, abortController);

  // 解析 SDK 合并配置，提取模型名（WS server 创建前暂存）
  let resolvedModel = CONFIG.modelName || '';
  try {
    const resolved = await (sdk as any).resolveSettings?.({
      cwd: CONFIG.projectPath,
      settingSources: CONFIG.settingSources,
    });
    if (resolved) {
      const effective = resolved.effective || {};
      resolvedModel = effective.model || resolvedModel || '';

      // 提取配置来源文件，按优先级排列
      const provenance: Record<string, { source?: string; path?: string }> = resolved.provenance || {};
      const fileKeys = new Map<string, { count: number; source: string }>();
      for (const key of Object.keys(provenance)) {
        const p = provenance[key];
        const fp = p?.path;
        if (fp) {
          const entry = fileKeys.get(fp);
          if (entry) { entry.count++; }
          else { fileKeys.set(fp, { count: 1, source: p.source || 'unknown' }); }
        }
      }
      if (fileKeys.size > 0) {
        const priority: Record<string, number> = { user: 0, project: 1, local: 2 };
        const sorted = Array.from(fileKeys.entries()).sort((a, b) => {
          return (priority[a[1].source] || 0) - (priority[b[1].source] || 0);
        });
        console.log('[cc-companion] 配置来源文件 (优先级 低→高):');
        sorted.forEach(([fp, info], i) => {
          console.log('  ' + (i + 1) + '. [' + info.source + '] ' + fp + ' (' + info.count + ' 项)');
        });
      } else {
        console.log('[cc-companion] 配置来源文件: (无)');
      }
    }
  } catch (err: any) {
    console.log(`[cc-companion] 解析合并配置失败 (非致命): ${err.message}`);
  }

  let bus: MessageBus;

  let currentProc = createResponseProcessor(
    queryIterator,
    CONFIG.projectPath,
    (msg) => bus.publishSdkMessage(msg as Record<string, unknown>),
    onInit,
  );
  let processResponses = currentProc.process;

  function startNewSession() {
    abortController = new AbortController();
    const result = createSession(sdk, CONFIG, abortController);
    inputStream = result.inputStream;
    queryIterator = result.queryIterator;
    currentProc = createResponseProcessor(
      queryIterator,
      CONFIG.projectPath,
      (msg) => bus.publishSdkMessage(msg as Record<string, unknown>),
      onInit,
    );
    processResponses = currentProc.process;
    console.log('[cc-companion] 新会话已创建');
  }

  const server = createWSServer(
    CONFIG.port,
    CONFIG.host,
    CONFIG.token,
    // onEvent — RimWorld 游戏事件（C# 端已格式化文本）
    (wsMessage) => {
      const payload = (wsMessage.payload || {}) as Record<string, unknown>;
      const text: string = (payload.text as string) || '';

      // 直接广播用户消息给聊天页面（不经 SDK 转发）
      if (text) {
        bus.publishUserMessage(text);
      }

      // 提取结构化殖民地统计并直接广播给聊天页（不经 SDK 转发）
      const colonyStats = payload.colonyStats as Record<string, unknown> | undefined;
      if (colonyStats && (colonyStats.colonistCount !== undefined || colonyStats.avgMood !== undefined)) {
        bus.publishColonyStats(colonyStats as any);
      }

      // 提取 TODO 状态并直接广播给聊天页（纯 UI 消息，不经 SDK）
      const todoItems = payload.todoItems as Array<Record<string, unknown>> | undefined;
      if (todoItems) {
        bus.publishTodoState(todoItems);
      }
      if (wsMessage.event === 'todo-state') return;

      console.log(`[event] ${wsMessage.event || 'unknown'}: ${text.substring(0, 100)}`);

      // Token 预算检查（Companion 侧辅助 enforcement）
      if (CONFIG.tokenBudgetLimit > 0 && CONFIG.tokenBudgetUsed >= CONFIG.tokenBudgetLimit) {
        if (CONFIG.tokenBudgetAction === 'Block') {
          console.log(`[cc-companion] Token 预算已用尽(${CONFIG.tokenBudgetUsed}/${CONFIG.tokenBudgetLimit})，阻止消息`);
          bus.publishError(`Token 预算已用尽 (${CONFIG.tokenBudgetUsed}/${CONFIG.tokenBudgetLimit})`);
          return;
        }
        console.log(`[cc-companion] Token 预算已用尽，但为 Warn 模式，继续发送`);
      }

      inputStream.enqueue({
        type: 'user',
        message: { role: 'user', content: text },
      });
      processResponses().catch(() => {});
    },
    // onStatusChange
    (status) => {
      if (status.status === 'connected') {
        console.log(`[cc-companion] RimWorld 已连接: ${typeof status.client === 'string' ? status.client : status.client?.name || 'unknown'}`);
        if (idleTimer) { clearTimeout(idleTimer); idleTimer = null; }
      } else if (status.status === 'disconnected') {
        console.log('[cc-companion] RimWorld 已断开');
        if (CONFIG.idleTimeout > 0) {
          idleTimer = setTimeout(() => {
            console.log(`[cc-companion] 断开后 ${CONFIG.idleTimeout / 1000}s 无重连，自动退出`);
            shutdown();
          }, CONFIG.idleTimeout);
        }
      }
    },
    // onAbort
    () => {
      abortController.abort();
      bus.publishAborted();
      startNewSession();
    },
  );

  // 初始化消息总线
  bus = new MessageBus((data) => server.broadcast(data));

  // 将解析后的模型名存入 CONFIG，供 ws-server 握手时推送
  CONFIG.resolvedModel = resolvedModel;

  // HTTP 路由 — 聊天页面
  if (CONFIG.chatPageEnabled && chatPageHtml) {
    setupChatHttp(server.httpServer, chatPageHtml);
  }

  // 启动首次处理
  processResponses().catch((err: any) => {
    console.error(`[cc-companion] SDK 处理异常: ${err.message}`);
  });

  // 6. 连接/断开超时
  let idleTimer: ReturnType<typeof setTimeout> | null = null;
  let disidleTimer: ReturnType<typeof setTimeout> | null = null;
  if (CONFIG.idleTimeout > 0) {
    idleTimer = setTimeout(() => {
      console.log(`[cc-companion] 空闲超时：${CONFIG.idleTimeout / 1000}s 内无客户端连接，自动退出`);
      shutdown();
    }, CONFIG.idleTimeout);
    console.log(`[cc-companion] ${CONFIG.idleTimeout / 1000}s 内无客户端连接将自动退出`);
  }

  // 7. PID 文件
  const pidFile = join(process.cwd(), '.pid');
  writeFileSync(pidFile, String(process.pid));
  console.log(`[cc-companion] PID ${process.pid} → ${pidFile}`);

  // 8. 关闭——直接 exit 走 RST，不产生 TIME_WAIT
  function shutdown() {
    console.log('\n[cc-companion] 正在关闭...');
    if (idleTimer) { clearTimeout(idleTimer); idleTimer = null; }
    if (disidleTimer) { clearTimeout(disidleTimer); disidleTimer = null; }
    try { unlinkSync(pidFile); } catch {}
    process.exit(0);
  }

  process.on('SIGINT', shutdown);
  process.on('SIGTERM', shutdown);

  console.log('[cc-companion] 就绪，等待 RimWorldMCP 连接...');
  console.log(`[cc-companion] WebSocket: ws://${CONFIG.host}:${CONFIG.port}`);
  if (CONFIG.chatPageEnabled) {
    console.log(`[cc-companion] 聊天页面: http://${CONFIG.host}:${CONFIG.port}/`);
  }
  console.log('');
}

function writeSettingFile(filePath: string, content: string): void {
  try {
    const dir = dirname(filePath);
    if (!existsSync(dir)) mkdirSync(dir, { recursive: true });
    writeFileSync(filePath, content, 'utf-8');
    console.log(`[cc-companion] 写出 settings: ${filePath}`);
  } catch (err: any) {
    console.error(`[cc-companion] 写出 settings 失败: ${filePath} — ${err.message}`);
  }
}

main().catch((err: any) => {
  console.error(`[cc-companion] 致命错误: ${err.message}`);
  process.exit(1);
});
