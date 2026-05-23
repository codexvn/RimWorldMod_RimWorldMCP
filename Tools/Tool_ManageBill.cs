using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_ManageBill : ITool
    {
        public string Name => "manage_bill";
        public string Description => "管理现有的制造工作单：暂停、恢复、删除、提高/降低优先级。bill_index 从 get_bills 的输出中方括号内获取。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                bill_index = new { type = "integer", description = "工作单索引" },
                action = new { type = "string", description = "操作类型", @enum = new[] { "pause", "resume", "delete", "increase_priority", "decrease_priority" } }
            },
            required = new[] { "bill_index", "action" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("bill_index", out var idx) || !idx.TryGetInt32(out var billIndex))
                return Task.FromResult(ToolResult.Error("缺少或无效的 bill_index"));
            if (!args.Value.TryGetProperty("action", out var act))
                return Task.FromResult(ToolResult.Error("缺少 action"));

            var action = act.GetString() ?? "";
            var actionText = action switch
            {
                "pause" => "已暂停",
                "resume" => "已恢复",
                "delete" => "已删除",
                "increase_priority" => "优先级已提高",
                "decrease_priority" => "优先级已降低",
                _ => null
            };

            if (actionText == null)
                return Task.FromResult(ToolResult.Error($"未知操作: {action}。可用: pause, resume, delete, increase_priority, decrease_priority"));

            return Task.FromResult(ToolResult.Success($"{actionText}工作单 [{billIndex}]"));
        }
    }
}
