using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_SetWorkPriority : ITool
    {
        public string Name => "set_work_priority";
        public string Description => "设置殖民者的工作优先级 (0-4)。0=不分配，1=最高优先，4=最低优先。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                colonist_name = new { type = "string", description = "殖民者名称" },
                work_type = new { type = "string", description = "工作类型", @enum = new[] { "Crafting", "Cooking", "Construction", "Mining", "Growing", "Research", "Smithing", "Tailoring", "Hauling", "Cleaning", "Warden", "Hunting", "Art", "PlantCutting", "Doctor", "Patient", "Firefighter" } },
                priority = new { type = "integer", description = "优先级: 0=不分配, 1=最高, 2=高, 3=普通, 4=最低", minimum = 0, maximum = 4 }
            },
            required = new[] { "colonist_name", "work_type", "priority" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("colonist_name", out var cn)) return Task.FromResult(ToolResult.Error("缺少 colonist_name"));
            if (!args.Value.TryGetProperty("work_type", out var wt)) return Task.FromResult(ToolResult.Error("缺少 work_type"));
            if (!args.Value.TryGetProperty("priority", out var p) || !p.TryGetInt32(out var priority)) return Task.FromResult(ToolResult.Error("缺少 priority"));
            if (priority < 0 || priority > 4) return Task.FromResult(ToolResult.Error("priority 必须在 0-4 之间"));

            var colonist = cn.GetString() ?? "";
            var workType = wt.GetString() ?? "";
            var priorityText = priority == 0 ? "不分配" : $"优先级 {priority}";
            return Task.FromResult(ToolResult.Success($"已将 {colonist} 的 {workType} 设为 {priorityText}。"));
        }
    }
}
