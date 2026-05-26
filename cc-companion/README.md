# Claude Code — RimWorldMCP 游戏 AI 助手

TypeScript 项目，WebSocket 接收游戏事件，Claude Agent SDK 驱动 AI 操控 RimWorld。`tsx` 运行时，所有依赖自包含。

## 快速开始

```bash
npm install          # 安装依赖（Claude Agent SDK + tsx + ws）
npm start            # tsx companion/companion.ts
```

游戏设置中开启"自动启动本地 Companion"，RimWorld 加载存档时自动 spawn。也可在设置面板点"安装 Claude Code 依赖"。

## 文件结构

```
cc-companion/
├── companion/             # 入口 + 配置 + SDK 加载
│   ├── companion.ts       # 编排入口
│   ├── config.ts          # 配置（CLI + 环境变量）
│   └── sdk-loader.ts      # SDK 加载
├── bridge/                # 桥接层（WS + SDK 会话）
│   ├── ws-server.ts       # WebSocket Server
│   └── session.ts         # SDK 会话 + AsyncStream + 响应广播
├── chat/                  # 聊天 UI
│   ├── chat-http.ts       # HTTP 路由
│   └── chat-page.ts       # 聊天页面 HTML
├── rimworld/
│   └── context.ts         # 系统提示词加载（Prompt.md）
├── Prompt.md              # AI 行为提示词
├── package.json
├── tsconfig.json
└── README.md
```

## 数据流

```
RimWorld (C#)                  CC Companion (Node.js)       Claude API
    │                                │                         │
    │ 游戏事件(WS)                     │  SDK query()            │
    │──────────────────────────────▶ │────────────────────────▶│
    │                                │                         │
    │ 聊天窗 ◀─ WS broadcast ────────│  ◀── assistant/tool ────│
    │                                │                         │
    │  MCP Server :9877 ◀────────────│──── tools/call ─────────│
```

## 参数来源

敏感数据（API Key、Base URL、MCP 配置）由 C# 写入 `{sessionsDir}/.claude/settings.json`，SDK 自动读取。非敏感参数可通过 CLI/env var 覆盖：

| 环境变量 | CLI 参数 | 默认值 | 说明 |
|----------|----------|--------|------|
| `CCB_HOST` | `--host` | `127.0.0.1` | WebSocket 监听地址 |
| `CCB_PORT` | `--port` | `19999` | WebSocket 监听端口 |
| `CCB_AUTH_TOKEN` | `--token` | 无 | WS 握手认证 |
| `CCB_MODEL_NAME` | `--model-name` | 空 | 模型名称 |
| `CCB_IDLE_TIMEOUT` | `--idle-timeout` | 不传则永不退出 | 空闲超时自动退出（ms） |
| `CCB_SETTING_SOURCES` | `--setting-sources` | `user,project,local` | settings 加载源 |
| `RIMWORLD_PROJECT_PATH` | `--project-path` | `process.cwd()` | SDK 项目目录 |

API Key、Base URL、权限模式、MCP 服务等敏感/复杂配置由 C# 写入 `.claude/settings.json`，SDK 自动读取，不通过 CLI 或环境变量传递。

## 会话存储

SDK 会话持久化到 `~/.claude/projects/<sanitizedCwd>/<sessionId>.jsonl`。不同存档通过不同的 `RIMWORLD_PROJECT_PATH` 实现隔离。
