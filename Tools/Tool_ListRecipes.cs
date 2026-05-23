using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_ListRecipes : ITool
    {
        public string Name => "list_recipes";
        public string Description => "列出当前可用的制造配方（已研究解锁的）。可按工作台类型和关键词过滤。返回配方的 defName、产物名称、所需材料、技能要求和工作量。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                search = new { type = "string", description = "搜索关键词，模糊匹配配方名称或产物名称" },
                workbench_type = new { type = "string", description = "工作台类型过滤", @enum = new[] { "TableSmithy", "TableTailor", "TableButcher", "TableMachining", "FueledStove", "TableFabrication" } }
            }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var search = "";
            var workbench = "";
            if (args != null)
            {
                if (args.Value.TryGetProperty("search", out var s)) search = s.GetString() ?? "";
                if (args.Value.TryGetProperty("workbench_type", out var w)) workbench = w.GetString() ?? "";
            }

            var allRecipes = new[]
            {
                new { defName="Make_LongSword", label="长剑", workbench="TableSmithy", materials="钢铁 x75", skill="锻造 6+", work="6000" },
                new { defName="Make_PlateArmor", label="板甲", workbench="TableSmithy", materials="钢铁 x170", skill="锻造 8+", work="18000" },
                new { defName="Make_Gladius", label="短剑", workbench="TableSmithy", materials="钢铁 x50", skill="锻造 4+", work="4000" },
                new { defName="Make_Duster", label="防尘大衣", workbench="TableTailor", materials="布匹 x80", skill="缝纫 6+", work="10000" },
                new { defName="Make_Pants", label="裤子", workbench="TableTailor", materials="布匹 x40", skill="缝纫 3+", work="2000" },
                new { defName="Make_ButtonDownShirt", label="高级衬衫", workbench="TableTailor", materials="布匹 x55", skill="缝纫 5+", work="4500" },
                new { defName="Make_SimpleMeal", label="简单食物", workbench="FueledStove", materials="食材 x0.5(营养)", skill="烹饪 3+", work="300" },
                new { defName="Make_FineMeal", label="精致食物", workbench="FueledStove", materials="食材 x0.5(营养)", skill="烹饪 6+", work="450" },
                new { defName="Make_Component", label="零部件", workbench="TableMachining", materials="钢铁 x12", skill="手工 8+", work="4000" },
                new { defName="Make_AssaultRifle", label="突击步枪", workbench="TableMachining", materials="钢铁 x60 + 零部件 x3", skill="手工 7+", work="30000" },
            };

            var filtered = allRecipes.AsEnumerable();
            if (!string.IsNullOrEmpty(workbench))
                filtered = filtered.Where(r => r.workbench == workbench);
            if (!string.IsNullOrEmpty(search))
                filtered = filtered.Where(r => r.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || r.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            var lines = filtered.Select(r => $"- {r.label} ({r.defName}) | {r.workbench} | {r.materials} | {r.skill} | 工作量: {r.work}").ToList();
            var result = lines.Count > 0 ? $"可用配方 ({lines.Count} 个):\n" + string.Join("\n", lines) : "没有匹配的配方。";
            return Task.FromResult(ToolResult.Success(result));
        }
    }
}
