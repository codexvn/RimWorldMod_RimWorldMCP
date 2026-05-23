# RimWorldMCP 设计文档

## 设计目标

将 RimWorld 游戏操作封装为 MCP Tool，让 LLM 可以：
1. 查询游戏状态（殖民者、资源、研究、工作单、健康、防御）
2. 执行游戏操作（建造、制造、研究、手术、装备、征召）
3. 获取领域知识（Skill 系统提供领域最佳实践）

## 架构

单进程 mod 内嵌架构——MCP 服务作为 RimWorld mod DLL 运行在游戏进程内：

```
┌──────────────┐   SSE / Streamable HTTP   ┌──────────────────────┐
│  LLM Client  │ ◄─────────────────────────►│   RimWorld (Unity)    │
│  (Claude等)  │    JSON-RPC 2.0            │     net472            │
└──────────────┘                            │                       │
                                            │  ┌─────────────────┐  │
                                            │  │ RimWorldMCP.dll │  │
                                            │  │  (mod 内嵌)     │  │
                                            │  └─────────────────┘  │
                                            └──────────────────────┘
```

**关键差异**（对比原两进程设计）：
- 不再有独立的 net8.0 MCP 服务器进程
- 不再通过 HTTP 中转调用游戏 API
- Tool 直接引用 `Assembly-CSharp.dll`，调用 `Find.*`、`DefDatabase<>` 等 RimWorld API
- 入口为 `GameComponent`（反射自动发现），非 `Program.cs`
- 不支持 stdio 传输（Unity 占用 stdin/stdout）

### 分层设计

| 层 | 职责 | 关键文件 |
|----|------|---------|
| **GameComponent** | Mod 入口，MCP 服务生命周期管理 | `GameComponent_McpServer.cs` |
| **McpCommandQueue** | 线程安全命令队列，写操作调度到主线程 | `McpCommandQueue.cs` |
| **Transport** | 传输抽象：HttpListener 后台线程收发消息 | `ITransport.cs` + `SseTransport.cs` + `StreamableHttpTransport.cs` |
| **MCP Protocol** | JSON-RPC 2.0 协议调度 | `McpServer.cs`, `McpMessage.cs` |
| **Tool** | 游戏操作封装，直接调用 RimWorld API | `ITool.cs`, `ToolRegistry.cs`, 21 个 `Tool_*.cs` |
| **Skill** | 领域知识文件系统 | `SkillInfo.cs`, `SkillRegistry.cs`, 6 个 `.md` |

### 传输层与协议层分离

`ITransport` 不关心 MCP 协议，只负责收发字符串消息。
`McpServer` 不关心传输方式，只管 JSON-RPC 解析和分发。

SSE 和 Streamable HTTP 均通过 mod 内 `HttpListener` 后台线程实现，共享同一套 Tool/Skill/McpServer。

### 线程安全模型

- **只读 Tool**：在 HttpListener 后台线程直接访问游戏状态（数据结构在帧间隙稳定）
- **写操作 Tool**：通过 `McpCommandQueue` 将操作入队，`GameComponentUpdate()` 在主线程逐帧处理
- **超时**：每个入队操作等待主线程处理最长 5 秒，超时返回错误

### 数据流

```
LLM Client
  │  POST /mcp  {"method":"tools/call","params":{"name":"get_colonists"}}
  ▼
HttpListener (后台线程)
  │  OnMessage 事件
  ▼
McpServer.DispatchAsync()
  │  tools/call → ToolRegistry.ExecuteAsync()
  ▼
Tool_GetColonists.ExecuteAsync()
  │  PawnsFinder.AllMaps_FreeColonistsSpawned  ← 直接调用 RimWorld API
  │  (只读，在 HttpListener 线程)
  ▼
ToolResult → ContentItem → JsonRpcResponse → HTTP Response
```

## Tool 分类设计

Tool 按玩家实际游玩操作分为 7 大类：

| 类别 | Tool 数量 | 说明 |
|------|----------|------|
| 通用查询 | 2 | 游戏全局快照、资源库存 |
| 制造 | 4 | 配方查询、单据创建/查看/管理 |
| 建造 | 2 | 放置蓝图、快速建造房间 |
| 研究 | 3 | 项目列表、进度查询、设置当前项目 |
| 殖民者需求 | 3 | 殖民者信息、需求详情、工作优先级 |
| 医疗 | 2 | 健康报告、安排手术 |
| 战斗 | 3 | 装备武器、征召、防御评估 |
| Skill | 2 | 列出领域知识、激活获取内容 |

### 安全分级

- **ReadOnly**: `get_*`, `list_*` 系列纯查询，无任何副作用
- **Normal**: `create_*`, `set_*`, `designate_*`, `manage_*`, `draft_*`, `equip_*` 等有游戏状态变更的操作，通过 McpCommandQueue 调度到主线程
- **ConfirmDestructive**: `schedule_operation`（手术有失败致死风险，需确认）
- **Forbidden**: 不为此类操作实现 Tool（攻击、处决、修改派系关系等）

## Skill 系统设计

Skill 提供领域知识注入，帮助 LLM 在特定场景下做出更好的决策。

### 文件格式（YAML frontmatter + Markdown）
```markdown
---
name: skill-name
description: 简短描述
---

# 标题
正文内容（Markdown 格式）
```

### 工作流程
1. `SkillRegistry.LoadFromDirectory()` 在启动时扫描 `Skills/` 目录
2. 解析每个 `.md` 的 frontmatter 提取 `name` 和 `description`
3. LLM 调用 `get_skills` 查看可用 Skill 列表
4. LLM 根据场景调用 `active_skill(name)` 获取完整内容
5. Skill 内容作为后续决策的知识基础

### 已有 Skill（6 个）
| Skill | 内容 |
|-------|------|
| `equipment-crafting` | 装备制造策略、品质控制、材料选择 |
| `colony-management` | 殖民地管理、工作分配、资源规划 |
| `combat-preparation` | 战斗准备、阵地部署、武器射程 |
| `base-building` | 基地布局设计、13x13 标准间、材料选择 |
| `research-management` | 科技树优先级、研究资源配置 |
| `medical-care` | 手术风险分析、植入体策略、药物使用 |

## 传输层

| 特性 | SSE | Streamable HTTP |
|------|-----|-----------------|
| 端点 | GET /sse + POST /message | POST /mcp |
| 规范 | MCP 2024-11-05 (旧版 SSE) | MCP 2025-03-26+ |
| 多客户端 | 是 | 是 |
| 使用场景 | 兼容旧版 MCP 客户端 | 现代部署 |

不支持 stdio（Unity 占用 stdin/stdout，mod DLL 无法直接使用）。
`StdioTransport.cs` 保留用于独立调试场景。

## GameComponent 生命周期

```
StartedNewGame() / LoadedGame()
  └─→ 加载 SkillRegistry (Skills/*.md)
  └─→ 创建 ToolRegistry，注册 21 个 Tool
  └─→ 创建 McpServer
  └─→ 创建 ITransport (StreamableHttpTransport，端口 9877)
  └─→ 启动 Transport

GameComponentUpdate() (每帧)
  └─→ McpCommandQueue.ProcessPending() 处理待执行命令

ExposeData()
  └─→ 保存/加载 MCP 服务配置（预留）
```

## 关键设计决策

1. **mod 内嵌而非独立进程**：简化部署，一个 DLL 搞定，无需管理两进程生命周期
2. **自实现 MCP 协议**：零业务 NuGet 依赖，仅 `System.Text.Json` 用于 JSON 序列化
3. **直接调用 RimWorld API**：Tool 引用 `Assembly-CSharp.dll`，不走 HTTP 中转，降低延迟和复杂度
4. **McpCommandQueue 线程安全**：写操作入队主线程执行，5 秒超时，避免游戏崩溃
5. **Skill 用 .md 文件**：简单直接，LLM 友好
6. **中文注释 + 英文标识符**：符合用户偏好
