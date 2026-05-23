# RimWorldMCP 设计文档

## 设计目标

将 RimWorld 游戏操作封装为 MCP Tool，让 LLM 可以：
1. 查询游戏状态（殖民者、资源、研究、工作单、健康、防御）
2. 执行游戏操作（建造、制造、研究、手术、装备、征召）
3. 获取领域知识（Skill 系统提供领域最佳实践）

## 架构

```
┌──────────────┐   MCP stdio/SSE/HTTP   ┌─────────────┐    HTTP     ┌──────────────────┐
│  LLM Client  │ ◄─────────────────────► │ RimWorldMCP  │ ◄─────────► │   RimWorld       │
│  (Claude等)  │    JSON-RPC 2.0        │  (net8.0)   │  REST API  │   (Unity, net472)│
└──────────────┘                        └─────────────┘            └──────────────────┘
                                          本项目                      F:\RiderProjects\
                                                                     Assembly-CSharp\
                                                                     (反编译源码)
```

**游戏源码路径**: `F:\RiderProjects\Assembly-CSharp\`
- 项目类型: Class Library, net472, C# 8
- 游戏内 API 层计划位置: `Assembly-CSharp/LLAma/` — GameComponent + HttpListener

### 分层设计

| 层 | 职责 | 关键文件 |
|----|------|---------|
| **Transport** | 传输抽象：接收/发送字符串消息 | `ITransport.cs` + 3 个实现 |
| **MCP Protocol** | JSON-RPC 2.0 协议调度 | `McpServer.cs`, `McpMessage.cs` |
| **Tool** | 游戏操作封装 | `ITool.cs`, `ToolRegistry.cs`, 21 个 `Tool_*.cs` |
| **Skill** | 领域知识文件系统 | `SkillInfo.cs`, `SkillRegistry.cs`, 6 个 `.md` |
| **RimWorldApi** | 游戏 HTTP API 客户端 | `RimWorldClient.cs` (stub) |

### 传输层与协议层分离

`ITransport` 不关心 MCP 协议，只负责收发字符串消息。
`McpServer` 不关心传输方式，只管 JSON-RPC 解析和分发。

这样传输方式可以独立切换（`--transport stdio|sse|http`），不影响业务逻辑。

### Tool 与数据源解耦

当前 21 个 Tool 使用 mock 数据（硬编码的示例信息）。
后续接入 `RimWorldClient` 调用真实游戏 API 后，只需修改各 Tool 的 `ExecuteAsync` 方法，其余架构不变。

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
- **Normal**: `create_*`, `set_*`, `designate_*`, `manage_*`, `draft_*`, `equip_*` 等有游戏状态变更的操作
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

### 与 MCP prompts 的对比
| 方案 | 优点 | 缺点 |
|------|------|------|
| 自建 Skill (.md 文件) | 简单、灵活、无需协议支持 | 非标准协议，仅此项目使用 |
| MCP prompts 协议 | 标准协议，客户端原生支持 | 参数化复杂，内容定义方式不同 |

当前采用 `.md` 文件方案，后续可选迁移到 MCP prompts。

## 传输层三种模式对比

| 特性 | stdio | SSE | Streamable HTTP |
|------|-------|-----|-----------------|
| 端点 | stdin/stdout | GET /sse + POST /message | POST /mcp |
| 客户端支持 | Claude Desktop 默认 | 旧版 MCP 客户端 | 新版 MCP 规范 |
| 多客户端 | 否（单一进程） | 是 | 是 |
| 服务器推送 | 同步响应 | 事件流异步推送 | 流式响应 |
| 使用场景 | 本地开发/个人使用 | 团队共享/远程访问 | 现代部署 |
| MCP 规范 | 2024-11-05 | 2024-11-05 (旧版 SSE) | 2025-03-26+ |

## 与 RimWorld 的对接方案

### 游戏源码
- 路径: `F:\RiderProjects\Assembly-CSharp\`
- 项目: `Assembly-CSharp.csproj`（Class Library, net472, C# 8）
- 计划 API 层目录: `Assembly-CSharp/LLAma/`

### 架构
```
RimWorld (Unity, net472)
  └── GameApiServer (GameComponent + HttpListener)
        ├── GET  /api/context       → 游戏状态 JSON
        ├── GET  /api/recipes       → 可用配方
        ├── GET  /api/bills         → 工作单列表
        ├── GET  /api/resources     → 资源统计
        ├── GET  /api/colonists     → 殖民者详情
        ├── GET  /api/research      → 研究进度
        ├── GET  /api/health        → 健康报告
        ├── GET  /api/defense       → 防御状态
        └── POST /api/tools/{name}  → 执行写入操作
```

### 线程安全
- 只读端点（GET）在 HttpListener 线程直接处理
- 写入端点（POST）将操作入队到主线程执行
- 写入结果通过轮询或事件通知返回

## 关键设计决策

1. **自实现 MCP 协议而非用官方 NuGet**：当前 net8.0 可以用官方 `ModelContextProtocol` NuGet（v1.3.0），待评估迁移收益后决策
2. **mock 数据先行**：先跑通全流程，再接入真实游戏 API
3. **Skill 用 .md 文件**：简单直接，LLM 友好
4. **中文注释 + 英文标识符**：符合用户偏好
