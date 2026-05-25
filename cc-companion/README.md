# CC Companion

RimWorldMCP 的 Claude Code 伴随进程。启动 WebSocket 服务器接收 RimWorld 游戏事件，使用 Claude Agent SDK 转发给 Claude。

## 快速开始

```bash
npm install
npm start
```

游戏设置中将桥接器类型切换为 "CC"，Companion 会自动启动（`CCAutoStart` 开关）。

## 配置

| 环境变量 | CLI 参数 | 默认值 | 说明 |
|----------|----------|--------|------|
| `ANTHROPIC_API_KEY` | - | - | API Key 认证 |
| `ANTHROPIC_AUTH_TOKEN` | - | - | Bearer Token 认证 |
| `CC_PORT` | `--port` | `19999` | WebSocket 监听端口 |
| `CC_TOKEN` | `--token` | 无 | 认证 token |
| `CC_MODEL` | `--model` | `sonnet` | 模型名称 |
| `MCP_PORT` | `--mcp-port` | `9877` | RimWorldMCP MCP 服务器端口 |

## 手动启动（调试用）

```bash
# 游戏设置中关闭 "自动启动本地 Companion"
# 然后手动运行：
node companion.js --port 19999

# 指定模型和 token
node companion.js --port 19999 --model opus --token my-secret
```

## 认证

按优先级自动查找：

1. 环境变量 `ANTHROPIC_API_KEY` / `ANTHROPIC_AUTH_TOKEN`
2. `~/.claude/settings.json`（Claude CLI 配置）
3. `~/.codemoss/config.json`（CC GUI 配置）

## 会话存储

Claude SDK 自动将会话持久化到 `~/.claude/projects/<项目>/<sessionId>.jsonl`。CC GUI 的历史面板可直接加载这些会话。
