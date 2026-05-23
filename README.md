# RimWorld MCP (RimWorldMCP)

RimWorld 1.6 模组——将游戏状态和操作暴露为 MCP (Model Context Protocol) Tool，让 AI 助手（Claude 等）可以直接查询殖民地状况并执行游戏操作。

## 解决的痛点

- **无法远程操控**：原版只能在游戏窗口内操作，离开电脑就无法管理殖民地
- **AI 辅助决策盲区**：LLM 不知道当前资源库存、殖民者状态、研究进度，只能给泛泛建议
- **重复操作繁琐**：批量创建制造单据、调整工作优先级、检查全殖民者健康需要大量点击
- **领域知识门槛**：新手不知何时该造什么装备、科技树怎么走、手术有多大风险

## 功能

- **21 个 MCP Tool**：覆盖通用查询、制造管理、建造规划、研究控制、殖民者需求、医疗、战斗七大类别
- **真实游戏状态查询**：通过 `Find.*`、`DefDatabase<>` 等 RimWorld API 直接读取殖民地数据（殖民者、资源、研究、工作单、健康、防御）
- **写操作线程安全**：所有修改操作通过 `McpCommandQueue` 调度到主线程执行，不破坏游戏稳定性
- **领域知识 Skill 系统**：内置 6 个领域知识文件（基地建造、殖民地管理、战斗准备、装备制造、医疗护理、科技研究），LLM 可在操作前激活获取最佳实践
- **SSE / Streamable HTTP 双传输**：支持 MCP 2024-11-05 SSE 协议和新版 Streamable HTTP 协议

### Tool 清单

| 类别 | Tool | 说明 |
|------|------|------|
| 通用查询 | `get_game_context` | 殖民地全局状态快照 |
| | `get_resources` | 资源库存报告 |
| 制造 | `list_recipes` | 列出可用配方 |
| | `create_production_bill` | 创建制造单据 |
| | `get_bills` | 查看工作单 |
| | `manage_bill` | 暂停/恢复/删除/调整优先级 |
| 建造 | `designate_build` | 放置建造蓝图 |
| | `designate_room` | 快速建造矩形房间 |
| 研究 | `list_research_projects` | 列出研究项目 |
| | `get_research_progress` | 研究进度查询 |
| | `set_research_project` | 设置当前研究 |
| 殖民者 | `get_colonists` | 殖民者信息 |
| | `get_colonist_needs` | 详细需求状态 |
| | `set_work_priority` | 设置工作优先级 |
| 医疗 | `get_colonist_health` | 健康报告 |
| | `schedule_operation` | 安排手术 |
| 战斗 | `equip_pawn` | 装备武器/衣物 |
| | `draft_pawn` | 征召/解除征召 |
| | `get_defense_status` | 防御状态报告 |
| Skill | `get_skills` | 列出可用领域知识 |
| | `active_skill` | 激活获取 Skill 内容 |

## Claude Desktop 配置

游戏启动后 MCP 服务自动运行在 `http://localhost:9877`。

```json
{
  "mcpServers": {
    "rimworld": {
      "type": "sse",
      "url": "http://localhost:9877/sse"
    }
  }
}
```

或使用 Streamable HTTP：

```json
{
  "mcpServers": {
    "rimworld": {
      "type": "http",
      "url": "http://localhost:9877/mcp"
    }
  }
}
```

## 开发

```bash
dotnet build
```

输出：`publish/1.6/Assemblies/RimWorldMCP.dll`

推荐创建目录链接到 RimWorld mod 目录：

```
mklink /D F:\SteamLibrary\steamapps\common\RimWorld\Mods\RimWorldMCP F:\RiderProjects\RimWorldMCP\publish
```

## 项目结构

```
RimWorldMCP/
├── GameComponent_McpServer.cs    # Mod 入口，管理 MCP 服务生命周期
├── McpCommandQueue.cs            # 线程安全命令队列
├── Transport/                    # 传输层 (SSE, Streamable HTTP, stdio)
├── Mcp/                          # MCP 协议层 (JSON-RPC 调度)
├── Tools/                        # 21 个 Tool 实现
├── Skills/                       # 6 个领域知识 .md 文件 + 加载器
└── About/                        # Mod 元数据
```

- **net472 Library**：与 RimWorld Unity 运行时一致
- **零 NuGet 业务依赖**：仅 `System.Text.Json` 用于 JSON 序列化
- **参考 Assembly-CSharp**：Tool 直接调用 `Find.*`、`DefDatabase<>` 等游戏 API
