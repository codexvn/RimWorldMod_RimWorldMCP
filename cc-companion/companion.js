#!/usr/bin/env node
/**
 * CC Companion — RimWorldMCP Claude Code 伴随进程
 *
 * 功能：
 *   1. 启动 WebSocket 服务器，接收 RimWorldMCP 推送的游戏事件
 *   2. 使用 Claude Agent SDK 创建长会话，将事件作为用户消息发给 Claude
 *   3. Claude 通过 MCP Tool 调用控制游戏（MCP Server 由 RimWorldMCP 自身提供）
 *   4. 会话持久化到 ~/.claude/projects/，CC GUI 历史面板可加载
 *
 * 用法：
 *   node companion.js [--port 19999] [--token xxx] [--model sonnet]
 *
 * 环境变量：
 *   ANTHROPIC_API_KEY 或 ANTHROPIC_AUTH_TOKEN — API 认证
 *   CC_MODEL — 模型名称 (默认 sonnet)
 *   CC_PORT — WebSocket 端口 (默认 19999)
 *   CC_TOKEN — 认证 token (可选)
 *   MCP_PORT — RimWorldMCP MCP 服务器端口 (默认 9877)
 *   RIMWORLD_PROJECT_PATH — 会话存储的项目路径 (默认 ~/rimworld)
 */

import { existsSync, readFileSync, writeFileSync, mkdirSync } from 'fs';
import { join, dirname } from 'path';
import { homedir } from 'os';
import { loadClaudeSdk } from './sdk-loader.js';
import { createWSServer } from './ws-server.js';

// ========== 配置 ==========

const CONFIG = {
  port: parseInt(process.env.CC_PORT || '19999'),
  token: process.env.CC_TOKEN || '',
  model: process.env.CC_MODEL || 'sonnet',
  mcpPort: parseInt(process.env.MCP_PORT || '9877'),
  projectPath: process.env.RIMWORLD_PROJECT_PATH || join(homedir(), 'rimworld'),
  permissionMode: process.env.CC_PERMISSION_MODE || 'bypassPermissions',
  maxTurns: parseInt(process.env.CC_MAX_TURNS || '500'),
};

// 解析命令行参数
for (let i = 2; i < process.argv.length; i++) {
  const arg = process.argv[i];
  if (arg === '--port' && process.argv[i + 1]) CONFIG.port = parseInt(process.argv[++i]);
  else if (arg === '--token' && process.argv[i + 1]) CONFIG.token = process.argv[++i];
  else if (arg === '--model' && process.argv[i + 1]) CONFIG.model = process.argv[++i];
  else if (arg === '--mcp-port' && process.argv[i + 1]) CONFIG.mcpPort = parseInt(process.argv[++i]);
  else if (arg === '--help') { printHelp(); process.exit(0); }
}

// ========== 认证 ==========

function setupApiAuth() {
  // 优先级: env var > ~/.claude/settings.json > ~/.codemoss/config.json
  if (process.env.ANTHROPIC_API_KEY || process.env.ANTHROPIC_AUTH_TOKEN) {
    console.log('[cc-companion] 认证: 环境变量');
    return;
  }

  // 尝试读取 Claude CLI 配置
  const settingsPath = join(homedir(), '.claude', 'settings.json');
  if (existsSync(settingsPath)) {
    try {
      const settings = JSON.parse(readFileSync(settingsPath, 'utf8'));
      if (settings.apiKeyHelper) {
        console.log('[cc-companion] 认证: apiKeyHelper (由 SDK 处理)');
        return;
      }
    } catch {}
  }

  // 尝试读取 CC GUI 配置
  const codemossConfig = join(homedir(), '.codemoss', 'config.json');
  if (existsSync(codemossConfig)) {
    try {
      const config = JSON.parse(readFileSync(codemossConfig, 'utf8'));
      const activeProvider = config.claude?.activeProvider;
      const providers = config.claude?.providers || {};
      const provider = activeProvider ? providers[activeProvider] : null;
      if (provider?.env) {
        if (provider.env.ANTHROPIC_API_KEY) {
          process.env.ANTHROPIC_API_KEY = provider.env.ANTHROPIC_API_KEY;
          console.log('[cc-companion] 认证: CC GUI provider');
          return;
        }
        if (provider.env.ANTHROPIC_AUTH_TOKEN) {
          process.env.ANTHROPIC_AUTH_TOKEN = provider.env.ANTHROPIC_AUTH_TOKEN;
          console.log('[cc-companion] 认证: CC GUI provider (Bearer)');
          return;
        }
      }
    } catch {}
  }

  console.warn('[cc-companion] 警告: 未找到 API 认证配置。请设置 ANTHROPIC_API_KEY 环境变量。');
}

// ========== 系统提示词 ==========

const RIMWORLD_SYSTEM_PROMPT = `

## RimWorld 游戏监控

你正在监控 RimWorld 游戏。你可以通过 MCP tool 查看游戏状态和控制游戏。

### 游戏事件处理规则

- 收到袭击通知时: 暂停游戏 → 调用 get_defense_status → 征召殖民者 → 指挥防御
- 收到死亡通知时: 检查殖民地状态 → 评估影响 → 给出建议
- 收到每日早报时: 做全面殖民地检查（资源/心情/威胁/研究）
- 收到负面事件时: 评估严重程度 → 决定是否需要人工介入
- 殖民者空闲时: 检查是否有待完成的工作 → 调整工作优先级

### MCP Tool 使用规则

- 所有写操作（建造/征召/标记）默认执行，不需要用户确认
- 操作前用 get_tile_detail 确认目标位置
- 坐标规则: pos_x=水平网格东西轴, pos_y=垂直网格南北轴（会被映射为 IntVec3.z）
- 涉及 Pawn 的操作使用 thingIDNumber 而非名称
- 区域操作使用左上角(pos) → 右下角(end)模式
- 写操作入队后等待 1-2 秒让游戏执行

### 当前 MCP Server

RimWorldMCP 运行在 http://localhost:${CONFIG.mcpPort}/mcp
`;

// ========== 会话存储 ==========

function getClaudeProjectsDir() {
  return join(homedir(), '.claude', 'projects');
}

function sanitizePath(p) {
  return p.replace(/[^a-zA-Z0-9]/g, '-').replace(/-+/g, '-');
}

// ========== AsyncStream (适配 SDK prompt 参数) ==========

class AsyncStream {
  constructor() {
    this.queue = [];
    this.readResolve = undefined;
    this.isDone = false;
    this.started = false;
  }

  [Symbol.asyncIterator]() {
    if (this.started) throw new Error('Stream can only be iterated once');
    this.started = true;
    return this;
  }

  async next() {
    if (this.queue.length > 0) return { done: false, value: this.queue.shift() };
    if (this.isDone) return { done: true, value: undefined };
    return new Promise((resolve) => { this.readResolve = resolve; });
  }

  enqueue(value) {
    if (this.readResolve) {
      const r = this.readResolve;
      this.readResolve = undefined;
      r({ done: false, value });
    } else {
      this.queue.push(value);
    }
  }

  done() {
    this.isDone = true;
    if (this.readResolve) {
      const r = this.readResolve;
      this.readResolve = undefined;
      r({ done: true, value: undefined });
    }
  }
}

// ========== 事件 → 用户消息转换 ==========

function gameEventToText(event) {
  const payload = event.payload || {};
  const text = payload.text || '';
  const category = payload.category || event.event || '';

  const icons = {
    RaidStart: '⚠️ [紧急]',
    RaidEnd: '✅',
    PawnDeath: '💀 [紧急]',
    NegativeEvent: '⚠️',
    AlertStart: '⚠️',
    DailyMorning: '🌅',
    IdleDetected: '⏳',
  };

  const instructions = {
    RaidStart: '\n请立即评估威胁并指挥防御。',
    PawnDeath: '\n请检查殖民地状态并评估影响。',
    DailyMorning: '\n请做全面的殖民地检查。',
    NegativeEvent: '\n请评估严重程度并给出应对建议。',
    AlertStart: '\n请检查并处理此警报。',
    IdleDetected: '\n请检查是否有待分配的工作。',
  };

  const icon = icons[category] || '📢';
  const suffix = instructions[category] || '';

  return `${icon} ${text}${suffix}`;
}

// ========== 主程序 ==========

async function main() {
  console.log('='.repeat(60));
  console.log('CC Companion — RimWorldMCP Claude Code 伴随进程');
  console.log('='.repeat(60));
  console.log(`配置: port=${CONFIG.port} model=${CONFIG.model} mcp_port=${CONFIG.mcpPort}`);
  console.log('');

  // 1. 设置认证
  setupApiAuth();

  // 2. 加载 SDK
  console.log('[cc-companion] 加载 Claude Agent SDK...');
  let sdk;
  try {
    sdk = await loadClaudeSdk();
  } catch (err) {
    console.error(`[cc-companion] SDK 加载失败: ${err.message}`);
    console.error('[cc-companion] 请确保已通过 CC GUI 安装 Claude SDK 依赖，或手动安装:');
    console.error('  npm install -g @anthropic-ai/claude-agent-sdk');
    process.exit(1);
  }

  // 3. 构建 query options
  const cwd = CONFIG.projectPath;
  try { mkdirSync(cwd, { recursive: true }); } catch {}

  const options = {
    cwd,
    model: CONFIG.model,
    permissionMode: CONFIG.permissionMode,
    maxTurns: CONFIG.maxTurns,
    enableFileCheckpointing: true,
    env: {
      ...process.env,
      CLAUDE_CODE_ENTRYPOINT: 'cli',
      USER_TYPE: 'external',
    },
    settingSources: ['user', 'project', 'local'],
    mcpServers: {
      rimworld: {
        type: 'http',
        url: `http://localhost:${CONFIG.mcpPort}/mcp`,
      },
    },
    systemPrompt: {
      type: 'preset',
      preset: 'claude_code',
      append: RIMWORLD_SYSTEM_PROMPT,
    },
    stderr: (data) => {
      const text = typeof data === 'string' ? data : data.toString();
      process.stderr.write(`[sdk] ${text}`);
    },
  };

  console.log(`[cc-companion] 项目目录: ${cwd}`);
  console.log(`[cc-companion] MCP Server: http://localhost:${CONFIG.mcpPort}/mcp`);
  console.log(`[cc-companion] 会话将存储在: ${getClaudeProjectsDir()}`);

  // 4. 创建 Claude 会话
  const inputStream = new AsyncStream();
  let queryIterator = null;
  let sessionId = 'pending';

  console.log('[cc-companion] 创建 Claude SDK 会话...');
  try {
    queryIterator = sdk.query({ prompt: inputStream, options });
    console.log('[cc-companion] SDK 会话已创建');
  } catch (err) {
    console.error(`[cc-companion] SDK 会话创建失败: ${err.message}`);
    process.exit(1);
  }

  // 5. 后台处理 Claude 响应
  let queryProcessing = false;
  async function processSDKResponses() {
    if (queryProcessing) return;
    queryProcessing = true;
    try {
      for await (const message of queryIterator) {
        const msgType = message?.type || 'unknown';

        if (msgType === 'system') {
          if (message.session_id && sessionId !== message.session_id) {
            sessionId = message.session_id;
            console.log(`[cc-companion] 会话 ID: ${sessionId}`);
            const sessionFile = join(getClaudeProjectsDir(), sanitizePath(cwd), `${sessionId}.jsonl`);
            console.log(`[cc-companion] 会话文件: ${sessionFile}`);
          }
        }

        if (msgType === 'assistant') {
          const content = message.message?.content;
          if (Array.isArray(content)) {
            for (const block of content) {
              if (block.type === 'text') {
                const text = block.text?.substring(0, 200) || '';
                console.log(`[assistant] ${text}${block.text?.length > 200 ? '...' : ''}`);
              } else if (block.type === 'tool_use') {
                console.log(`[tool_use] ${block.name}`);
              }
            }
          }
        }

        if (msgType === 'result') {
          const summary = message.subtype === 'success'
            ? '执行成功' : `执行失败: ${message.errors?.join(', ') || 'unknown'}`;
          console.log(`[result] ${summary}`);
        }
      }
    } catch (err) {
      console.error(`[cc-companion] SDK 响应处理错误: ${err.message}`);
    }
    queryProcessing = false;
  }

  // 启动后台处理 (不 await，让它在后台运行)
  processSDKResponses().catch(err => {
    console.error(`[cc-companion] SDK 处理异常: ${err.message}`);
  });

  // 6. 启动 WebSocket 服务器
  console.log('[cc-companion] 启动 WebSocket 服务器...');

  const server = createWSServer(
    CONFIG.port,
    CONFIG.token,
    // onEvent — 收到 RimWorld 游戏事件
    (wsMessage) => {
      const text = gameEventToText(wsMessage);
      console.log(`[event] ${wsMessage.event || 'unknown'}: ${text.substring(0, 100)}`);

      // 作为用户消息推送到 Claude SDK 会话
      inputStream.enqueue({
        type: 'user',
        message: {
          role: 'user',
          content: text,
        },
      });

      // 确保后台处理在运行
      processSDKResponses().catch(() => {});
    },
    // onStatusChange
    (status) => {
      if (status.status === 'connected') {
        console.log(`[cc-companion] RimWorld 已连接: ${status.client?.name || 'unknown'}`);
      } else if (status.status === 'disconnected') {
        console.log('[cc-companion] RimWorld 已断开');
      }
    }
  );

  // 7. 优雅关闭
  function shutdown() {
    console.log('\n[cc-companion] 正在关闭...');
    inputStream.done();
    server.close();
    setTimeout(() => process.exit(0), 2000);
  }

  process.on('SIGINT', shutdown);
  process.on('SIGTERM', shutdown);

  console.log('[cc-companion] 就绪，等待 RimWorldMCP 连接...');
  console.log(`[cc-companion] WebSocket: ws://127.0.0.1:${CONFIG.port}`);
  console.log('');
}

function printHelp() {
  console.log(`
CC Companion — RimWorldMCP Claude Code 伴随进程

用法: node companion.js [选项]

选项:
  --port <port>       WebSocket 端口 (默认 19999, 环境变量 CC_PORT)
  --token <token>     认证 token (可选, 环境变量 CC_TOKEN)
  --model <model>     模型名称 (默认 sonnet, 环境变量 CC_MODEL)
  --mcp-port <port>   RimWorldMCP MCP 端口 (默认 9877, 环境变量 MCP_PORT)
  --help              显示帮助

环境变量:
  ANTHROPIC_API_KEY        API Key 认证
  ANTHROPIC_AUTH_TOKEN     Bearer Token 认证
`);
}

main().catch(err => {
  console.error(`[cc-companion] 致命错误: ${err.message}`);
  process.exit(1);
});
