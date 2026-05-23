# RimWorldMCP

MCP (Model Context Protocol) 服务器，将 RimWorld 游戏状态和操作暴露为 LLM 可调用的 Tool。作为 RimWorld mod DLL 内嵌运行。

**游戏源码**: `F:\RiderProjects\Assembly-CSharp\`（RimWorld 反编译 C# 源码，net472 Class Library）

## 项目结构

```
RimWorldMCP/
├── GameComponent_McpServer.cs            # Mod 入口：GameComponent 子类，管理 MCP 服务生命周期
├── McpCommandQueue.cs                    # 线程安全命令队列（ConcurrentQueue + TaskCompletionSource）
├── Transport/                            # 传输层
│   ├── ITransport.cs                     # 抽象接口
│   ├── SseTransport.cs                   # GET /sse + POST /message（HttpListener 后台线程）
│   ├── StreamableHttpTransport.cs        # POST /mcp（新版协议）
│   └── StdioTransport.cs                 # stdin/stdout（保留，不在游戏内使用）
├── Mcp/                                  # MCP 协议层
│   ├── McpServer.cs                      # JSON-RPC 调度：initialize/tools/list/tools/call/resources
│   └── McpMessage.cs                     # 数据类型：请求/响应/Tool定义/资源
├── Tools/                                # 21 个 Tool（真实 RimWorld API 调用）
│   ├── ITool.cs                          # Tool 接口 + ToolResult
│   ├── ToolRegistry.cs                   # 注册表 + 执行调度 + 资源映射
│   └── Tool_*.cs                         # 各 Tool 实现
├── Skills/                               # 领域知识 Skill 系统
│   ├── SkillInfo.cs                      # Skill 数据模型
│   ├── SkillRegistry.cs                  # 加载 .md 文件、解析 frontmatter
│   └── *.md                              # 6 个 Skill 文件
└── About/
    └── About.xml                         # Mod 元数据
```

## 架构

单进程 mod 内嵌——LLM 通过 SSE 或 Streamable HTTP 连接游戏内的 MCP 服务。

- **net472 Library**：与 RimWorld Unity 运行时一致，`OutputType=Library`
- **引用 Assembly-CSharp.dll**：Tool 直接调用游戏 API（`Find.*`、`DefDatabase<>`、`PawnsFinder` 等）
- **GameComponent 入口**：反射自动发现，`StartedNewGame()` 时启动 HttpListener
- **线程安全**：只读 Tool 在 HttpListener 线程直接执行；写操作 Tool 通过 `McpCommandQueue` 调度到主线程
- **NuGet**: 仅 `System.Text.Json` 8.0.5（JSON 序列化）
- **输出**: `publish/1.6/Assemblies/RimWorldMCP.dll`

## 部署

```bash
# 构建
dotnet build

# 输出到 publish/1.6/Assemblies/
# 将整个 publish/ 目录放入 RimWorld Mods/RimWorldMCP/
# 或创建目录链接:
mklink /D F:\SteamLibrary\steamapps\common\RimWorld\Mods\RimWorldMCP F:\RiderProjects\RimWorldMCP\publish
```

游戏启动后，MCP 服务自动运行在 `http://localhost:9877`。

## Tool 清单（21 个，真实 API）

### 通用查询
| Tool | 说明 | 数据源 |
|------|------|--------|
| `get_game_context` | 游戏全局状态快照 | `Find.CurrentMap`, `Find.TickManager`, `Find.ResearchManager` |
| `get_resources` | 资源库存报告 | `map.resourceCounter.AllCountedAmounts` |

### 制造 (4)
| Tool | 说明 | 数据源/操作 |
|------|------|------------|
| `list_recipes` | 列出可用配方 | `DefDatabase<RecipeDef>.AllDefs` |
| `create_production_bill` | 创建制造单据 | `BillStack.AddBill()` (入队) |
| `get_bills` | 查看工作单状态 | `map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>()` |
| `manage_bill` | 管理单据（暂停/恢复/删除/优先级） | `bill.suspended`, `billStack.Delete()`, `billStack.Reorder()` (入队) |

### 建造 (2)
| Tool | 说明 | 数据源/操作 |
|------|------|------------|
| `designate_build` | 放置建造蓝图 | `GenConstruct.PlaceBlueprintForBuild()` (入队) |
| `designate_room` | 快速建造矩形房间 | 批量 `PlaceBlueprintForBuild()` (入队) |

### 研究 (3)
| Tool | 说明 | 数据源/操作 |
|------|------|------------|
| `list_research_projects` | 列出研究项目 | `DefDatabase<ResearchProjectDef>.AllDefsListForReading` |
| `get_research_progress` | 获取研究进度 | `Find.ResearchManager.GetProgress()` |
| `set_research_project` | 设置研究项目 | `Find.ResearchManager.SetCurrentProject()` (入队) |

### 殖民者需求 (3)
| Tool | 说明 | 数据源/操作 |
|------|------|------------|
| `get_colonists` | 殖民者信息 | `PawnsFinder.AllMaps_FreeColonistsSpawned` |
| `get_colonist_needs` | 详细需求状态 | `pawn.needs.AllNeeds` |
| `set_work_priority` | 设置工作优先级 | `pawn.workSettings.SetPriority()` (入队) |

### 医疗 (2)
| Tool | 说明 | 数据源/操作 |
|------|------|------------|
| `get_colonist_health` | 健康报告 | `pawn.health.hediffSet.hediffs` |
| `schedule_operation` | 安排手术 | `billStack.AddBill(Bill_Medical)` (入队) |

### 战斗 (3)
| Tool | 说明 | 数据源/操作 |
|------|------|------------|
| `equip_pawn` | 装备武器/衣物 | `pawn.equipment.AddEquipment()` / `pawn.apparel.Wear()` (入队) |
| `draft_pawn` | 征召/解除征召 | `pawn.drafter.Drafted` (入队) |
| `get_defense_status` | 防御状态报告 | `pawn.equipment.Primary`, `map.listerBuildings` |

### Skill (2)
| Tool | 说明 | 数据源 |
|------|------|--------|
| `get_skills` | 列出可用领域知识 | `SkillRegistry.GetAll()` |
| `active_skill` | 激活获取 Skill 内容 | `SkillRegistry.Get(name)` |

## Skill 系统

Skill 是领域知识文件（Markdown + YAML frontmatter），存放在 `Skills/` 目录。

| Skill | 内容 |
|-------|------|
| `equipment-crafting` | 装备制造策略、品质控制、材料选择 |
| `colony-management` | 殖民地管理、工作分配、资源规划 |
| `combat-preparation` | 战斗准备、阵地部署、武器射程 |
| `base-building` | 基地布局设计、13x13 标准间、材料选择 |
| `research-management` | 科技树优先级、研究资源配置 |
| `medical-care` | 手术风险分析、植入体策略、药物使用 |

## Claude Desktop 配置

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

- net472，仅 `System.Text.Json` NuGet 依赖
- 日志输出到 `Console.Error`（Transport 层）和 `Verse.Log`（GameComponent 层）
- Tool 返回值：`ToolResult` → `McpServer` 包装为 `{"content":[{"type":"text","text":"..."}]}`
- 写操作必须通过 `McpCommandQueue` 调度到主线程
- `dotnet build` → `publish/1.6/Assemblies/RimWorldMCP.dll`
