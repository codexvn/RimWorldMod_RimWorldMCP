#!/usr/bin/env tsx
/**
 * Claude Code — RimWorldMCP 游戏 AI 助手
 *
 * 用法: tsx companion.ts [--port 19999] [--token xxx] [--model sonnet]
 * 详见 README.md
 */

import { writeFileSync, unlinkSync } from 'fs';
import { join } from 'path';
import { CONFIG, parseArgs } from './config.js';
import { loadClaudeSdk } from './sdk-loader.js';
import { createWSServer } from './ws-server.js';
import { createSession, createResponseProcessor } from './session.js';

parseArgs(process.argv);

async function main(): Promise<void> {
  console.log('='.repeat(60));
  console.log('Claude Code — RimWorldMCP 游戏 AI 助手');
  console.log('='.repeat(60));
  console.log(`CWD: ${process.cwd()}`);
  console.log(`ARGV: ${process.argv.slice(2).join(' ')}`);
  const mcpUrl = (() => { try { const c = JSON.parse(CONFIG.mcpConfig); const s = Object.values(c)[0] as any; return s?.url || '?'; } catch { return '?'; } })();
  console.log(`配置: host=${CONFIG.host} port=${CONFIG.port} mcp=${mcpUrl}`);
  console.log(`会话目录: ${CONFIG.projectPath}`);
  console.log('');

  // 1. 加载 SDK
  console.log('[cc-companion] 加载 Claude Agent SDK...');
  let sdk: any;
  try {
    sdk = await loadClaudeSdk();
  } catch (err: any) {
    console.error(`[cc-companion] SDK 加载失败: ${err.message}`);
    process.exit(1);
  }

  // 3. 启动 WebSocket 服务器（先于 session，broadcast 需要引用）
  console.log('[cc-companion] 启动 WebSocket 服务器...');

  let abortController = new AbortController();
  let { inputStream, queryIterator } = createSession(sdk, CONFIG, abortController);
  let currentProc = createResponseProcessor(
    queryIterator,
    CONFIG.projectPath,
    (msg) => server.broadcast(JSON.stringify(msg)),
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
      (msg) => server.broadcast(JSON.stringify(msg)),
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
      const text: string = (wsMessage.payload as any)?.text || '';
      console.log(`[event] ${wsMessage.event || 'unknown'}: ${text.substring(0, 100)}`);
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
        if (connectTimer) { clearTimeout(connectTimer); connectTimer = null; }
      } else if (status.status === 'disconnected') {
        console.log('[cc-companion] RimWorld 已断开');
      }
    },
    // onAbort
    () => {
      abortController.abort();
      server.broadcast(JSON.stringify({ type: 'result', subtype: 'aborted' }));
      startNewSession();
    },
  );

  // 启动首次处理
  processResponses().catch((err: any) => {
    console.error(`[cc-companion] SDK 处理异常: ${err.message}`);
  });

  // 6. 连接超时
  let connectTimer: ReturnType<typeof setTimeout> | null = null;
  if (CONFIG.idleTimeout > 0) {
    connectTimer = setTimeout(() => {
      console.log(`[cc-companion] 空闲超时：${CONFIG.idleTimeout / 1000}s 内无客户端连接，自动退出`);
      shutdown();
    }, CONFIG.idleTimeout);
    console.log(`[cc-companion] ${CONFIG.idleTimeout / 1000}s 内无客户端连接将自动退出（--no-idle-timeout 可关闭）`);
  }

  // 7. PID 文件
  const pidFile = join(process.cwd(), '.pid');
  writeFileSync(pidFile, String(process.pid));
  console.log(`[cc-companion] PID ${process.pid} → ${pidFile}`);

  // 8. 关闭——直接 exit 走 RST，不产生 TIME_WAIT
  function shutdown() {
    console.log('\n[cc-companion] 正在关闭...');
    if (connectTimer) { clearTimeout(connectTimer); connectTimer = null; }
    try { unlinkSync(pidFile); } catch {}
    process.exit(0);
  }

  process.on('SIGINT', shutdown);
  process.on('SIGTERM', shutdown);

  console.log('[cc-companion] 就绪，等待 RimWorldMCP 连接...');
  console.log(`[cc-companion] WebSocket: ws://${CONFIG.host}:${CONFIG.port}`);
  console.log('');
}

main().catch((err: any) => {
  console.error(`[cc-companion] 致命错误: ${err.message}`);
  process.exit(1);
});
