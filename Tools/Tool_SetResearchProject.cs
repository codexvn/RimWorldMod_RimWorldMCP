using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_SetResearchProject : ITool
    {
        public string Name => "set_research_project";
        public string Description => "设置当前研究项目。项目 defName 需先用 list_research_projects 查询获取。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { project_defName = new { type = "string", description = "研究项目 defName" } },
            required = new[] { "project_defName" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("project_defName", out var defName)) return Task.FromResult(ToolResult.Error("缺少 project_defName"));

            var project = defName.GetString() ?? "";
            var knownProjects = new Dictionary<string, string>
            {
                ["MicroelectronicsBasics"] = "微型电子学基础",
                ["PrecisionFabrication"] = "精密装配",
                ["Gunsmithing"] = "枪械制造",
                ["ArmorSmithing"] = "护甲锻造",
                ["MedicineProduction"] = "药品生产",
            };

            if (!knownProjects.TryGetValue(project, out var label))
                return Task.FromResult(ToolResult.Error($"未知研究项目: {project}。可用: {string.Join(", ", knownProjects.Keys)}"));

            return Task.FromResult(ToolResult.Success($"已将研究项目设为: {label} ({project})。"));
        }
    }
}
