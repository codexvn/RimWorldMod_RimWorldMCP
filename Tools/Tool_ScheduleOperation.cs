using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_ScheduleOperation : ITool
    {
        public string Name => "schedule_operation";
        public string Description => "为殖民者安排手术或医疗操作。⚠ 手术有失败风险。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                colonist_name = new { type = "string", description = "要接受手术的殖民者名称" },
                recipe_defName = new { type = "string", description = "手术配方 defName，如 InstallBionicArm" },
                body_part = new { type = "string", description = "目标身体部位（可选）" }
            },
            required = new[] { "colonist_name", "recipe_defName" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("colonist_name", out var cn)) return Task.FromResult(ToolResult.Error("缺少 colonist_name"));
            if (!args.Value.TryGetProperty("recipe_defName", out var rn)) return Task.FromResult(ToolResult.Error("缺少 recipe_defName"));

            var colonist = cn.GetString() ?? "";
            var recipe = rn.GetString() ?? "";
            var bodyPart = "";
            if (args.Value.TryGetProperty("body_part", out var bp)) bodyPart = bp.GetString() ?? "";

            var knownSurgeries = new Dictionary<string, (string label, string risk, string requires)>
            {
                ["InstallBionicArm"] = ("安装仿生手臂", "低（失败致死率 2%）", "仿生手臂 x1 + 医药 x2"),
                ["InstallBionicLeg"] = ("安装仿生腿", "低（失败致死率 2%）", "仿生腿 x1 + 医药 x2"),
                ["InstallBionicEye"] = ("安装仿生眼", "极低（失败致死率 1%）", "仿生眼 x1 + 医药 x2"),
                ["InstallBionicHeart"] = ("安装仿生心脏", "中等（失败致死率 10%）", "仿生心脏 x1 + 医药 x3"),
                ["RemoveCarcinoma"] = ("切除癌症", "中等（失败致死率 8%）", "医药 x2"),
                ["Euthanize"] = ("安乐死", "零（必然成功）", "医药 x1"),
            };

            if (!knownSurgeries.TryGetValue(recipe, out var info))
                return Task.FromResult(ToolResult.Error($"未知手术配方: {recipe}。已知: {string.Join(", ", knownSurgeries.Keys)}"));

            var bpText = string.IsNullOrEmpty(bodyPart) ? "" : $" (部位: {bodyPart})";
            return Task.FromResult(ToolResult.Success(
                $"已为 {colonist} 安排手术: {info.label} ({recipe}){bpText}\n" +
                $"- 风险等级: {info.risk}\n- 所需材料: {info.requires}"));
        }
    }
}
