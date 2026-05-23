using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_ListResearchProjects : ITool
    {
        public string Name => "list_research_projects";
        public string Description => "列出所有研究项目，可按状态过滤（available 可研究, completed 已完成, all 全部）和关键词搜索。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                filter = new { type = "string", description = "过滤状态", @enum = new[] { "available", "completed", "all" } },
                search = new { type = "string", description = "搜索关键词" }
            }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var filter = "available"; var search = "";
            if (args != null)
            {
                if (args.Value.TryGetProperty("filter", out var f)) filter = f.GetString() ?? "available";
                if (args.Value.TryGetProperty("search", out var s)) search = s.GetString() ?? "";
            }

            var allProjects = new (string defName, string label, int cost, bool completed, string prereqs, string unlocks)[]
            {
                ("MicroelectronicsBasics", "微型电子学基础", 3000, false, "电力", "高级研究台"),
                ("PrecisionFabrication", "精密装配", 4000, false, "微型电子学基础", "高级零部件制造"),
                ("GeothermalPower", "地热发电", 4000, true, "微型电子学基础", "地热发电机"),
                ("AdvancedFabrication", "高级装配", 5000, false, "精密装配", "仿生部件"),
                ("Gunsmithing", "枪械制造", 2000, false, "机械加工", "突击步枪"),
                ("ArmorSmithing", "护甲锻造", 3000, false, "锻造", "板甲制造"),
                ("MedicineProduction", "药品生产", 3500, false, "微型电子学基础", "盘诺西林"),
                ("ShieldTechnology", "护盾技术", 5000, false, "高级装配", "个人护盾"),
                ("Mortars", "迫击炮", 3000, false, "枪械制造", "迫击炮"),
            };

            var filtered = allProjects.AsEnumerable();
            switch (filter) { case "available": filtered = filtered.Where(p => !p.completed); break; case "completed": filtered = filtered.Where(p => p.completed); break; }
            if (!string.IsNullOrEmpty(search))
                filtered = filtered.Where(p => p.label.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 || p.defName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);

            var lines = filtered.Select(p => $"- {(p.completed ? "✅已完成" : "⬜待研究")} {p.label} ({p.defName}) | 工作量: {p.cost} | 前置: {p.prereqs} | 解锁: {p.unlocks}").ToList();
            var result = lines.Count > 0 ? $"研究项目 ({lines.Count} 个):\n{string.Join("\n", lines)}" : "没有匹配的研究项目。";
            return Task.FromResult(ToolResult.Success(result));
        }
    }
}
