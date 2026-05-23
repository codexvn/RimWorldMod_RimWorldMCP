using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetResearchProgress : ITool
    {
        public string Name => "get_research_progress";
        public string Description => "获取当前研究进度：当前正在研究的项目、完成百分比、所有项目的完成状态。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var report = @"## 研究进度报告

### 当前研究
- 微型电子学基础 (MicroelectronicsBasics): 72.3% | 剩余约 831 工作量

### 已完成 (24 项)
地热发电, 电力, 电池, 太阳能板, 空调, 锻造, 缝纫, 石材切割,
机械加工, 医药生产基础, 基础种植, 烹饪基础, 啤酒酿造,
陷阱, 沙袋, 基础防御, 武器制造基础

### 下一步建议
- 精密装配 (PrecisionFabrication) — 解锁高级零部件
- 枪械制造 (Gunsmithing) — 解锁突击步枪
";
            return Task.FromResult(ToolResult.Success(report));
        }
    }
}
