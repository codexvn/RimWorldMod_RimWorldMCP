# RimWorldMCP

MCP (Model Context Protocol) 服务器，将 RimWorld 游戏状态和操作暴露为 LLM 可调用的 Tool。

**游戏源码**: `F:\RiderProjects\Assembly-CSharp\`（RimWorld 反编译 C# 源码，net472 Class Library）  
**游戏内 API 层**（计划）: `Assembly-CSharp/LLAma/` — GameComponent + HttpListener

## 项目结构

```
RimWorldMCP/
├── Program.cs                         # 入口：命令行解析、Tool 注册、启动
├── RimWorldMCP.csproj                 # net8.0，零 NuGet 依赖
├── Transport/                         # 传输层（收发字符串消息）
│   ├── ITransport.cs                  # 抽象接口
│   ├── StdioTransport.cs              # stdin/stdout（Claude Desktop 默认）
│   ├── SseTransport.cs                # GET /sse + POST /message
│   └── StreamableHttpTransport.cs     # POST /mcp（新版协议）
├── Mcp/                               # MCP 协议层（JSON-RPC 调度）
│   ├── McpServer.cs                   # 消息分发：initialize/tools/list/tools/call/resources
│   └── McpMessage.cs                  # 数据类型：请求/响应/Tool定义/资源
├── Tools/                             # 21 个 Tool（游戏操作封装）
│   ├── ITool.cs                       # Tool 接口
│   ├── ToolRegistry.cs                # 注册表 + 执行调度
│   └── Tool_*.cs                      # 各 Tool 实现
├── Skills/                            # 领域知识 Skill 系统
│   ├── SkillInfo.cs                   # Skill 数据模型
│   ├── SkillRegistry.cs               # 加载 .md 文件、解析 frontmatter
│   └── *.md                           # 6 个 Skill 文件
├── RimWorldApi/
│   └── RimWorldClient.cs              # 游戏 API HTTP 客户端（stub）
├── CLAUDE.md                          # 本文档
├── Design.md                          # 设计文档
└── TODO.md                            # 待办事项
```

## 运行

```bash
# stdio 模式（Claude Desktop 默认）
dotnet run --project F:\RiderProjects\RimWorldMCP -- --transport stdio

# SSE 模式（启动 HTTP 服务器，多客户端）
dotnet run --project F:\RiderProjects\RimWorldMCP -- --transport sse --port 9876

# Streamable HTTP 模式（新版 MCP 规范）
dotnet run --project F:\RiderProjects\RimWorldMCP -- --transport http --port 9876
```

## Tool 清单（21 个）

### 通用查询
| Tool | 说明 |
|------|------|
| `get_game_context` | 游戏全局状态快照 |
| `get_resources` | 资源库存报告 |

### 制造 (4)
| Tool | 说明 |
|------|------|
| `list_recipes` | 列出可用配方 |
| `create_production_bill` | 创建制造单据 |
| `get_bills` | 查看工作单状态 |
| `manage_bill` | 管理单据（暂停/恢复/删除/优先级） |

### 建造 (2)
| Tool | 说明 |
|------|------|
| `designate_build` | 放置建造蓝图 |
| `designate_room` | 快速建造矩形房间 |

### 研究 (3)
| Tool | 说明 |
|------|------|
| `list_research_projects` | 列出研究项目 |
| `get_research_progress` | 获取研究进度 |
| `set_research_project` | 设置研究项目 |

### 殖民者需求 (3)
| Tool | 说明 |
|------|------|
| `get_colonists` | 殖民者信息 |
| `get_colonist_needs` | 详细需求状态（心情/食物/休息/娱乐） |
| `set_work_priority` | 设置工作优先级 |

### 医疗 (2)
| Tool | 说明 |
|------|------|
| `get_colonist_health` | 健康报告（伤势/疾病/身体部位） |
| `schedule_operation` | 安排手术 |

### 战斗 (3)
| Tool | 说明 |
|------|------|
| `equip_pawn` | 装备武器/护甲 |
| `draft_pawn` | 征召/解除征召 |
| `get_defense_status` | 防御状态报告 |

### Skill (2)
| Tool | 说明 |
|------|------|
| `get_skills` | 列出可用领域知识 |
| `active_skill` | 激活获取 Skill 完整内容 |

## Skill 系统

Skill 是领域知识文件（Markdown + YAML frontmatter），存放在 `Skills/` 目录。LLM 可通过 `get_skills` 查看可用列表，通过 `active_skill(name)` 获取完整内容来指导决策。

### 已有 Skill（6 个）
| Skill | 内容 |
|-------|------|
| `equipment-crafting` | 装备制造策略、品质控制、材料选择 |
| `colony-management` | 殖民地管理、工作分配、资源规划 |
| `combat-preparation` | 战斗准备、阵地部署、武器射程 |
| `base-building` | 基地布局设计、13x13 标准间、材料选择 |
| `research-management` | 科技树优先级、研究资源配置 |
| `medical-care` | 手术风险分析、植入体策略、药物使用 |

### Skill 文件格式
```markdown
---
name: skill-name
description: 简短中文描述
---

# 标题
...Markdown 正文...
```

## 开发

- net8.0，零 NuGet 包依赖（仅用 `System.Text.Json` + `HttpListener`）
- 日志全部输出到 stderr（stdio 模式下 stdout 用于 MCP 通信）
- Tool 返回值格式：`{"content":[{"type":"text","text":"..."}]}`
- 当前 Tool 使用 mock 数据，后续接入 `RimWorldClient` 调用真实游戏 API

## Claude Desktop 配置

在 `claude_desktop_config.json` 中添加：

```json
{
  "mcpServers": {
    "rimworld": {
      "command": "dotnet",
      "args": ["run", "--project", "F:/RiderProjects/RimWorldMCP", "--", "--transport", "stdio"]
    }
  }
}
```
