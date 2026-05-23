using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_DraftPawn : ITool
    {
        public string Name => "draft_pawn";
        public string Description => "征召或解除征召殖民者。征召后殖民者进入战斗状态，中断当前工作。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                colonist_name = new { type = "string", description = "殖民者名称。留空则操作全部。" },
                drafted = new { type = "boolean", description = "true=征召, false=解除征召" }
            },
            required = new[] { "drafted" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("drafted", out var d) || d.ValueKind != JsonValueKind.True && d.ValueKind != JsonValueKind.False)
                return Task.FromResult(ToolResult.Error("缺少 drafted（需要 true 或 false）"));

            var drafted = d.GetBoolean();
            var colonist = "全体殖民者";
            if (args.Value.TryGetProperty("colonist_name", out var cn)) colonist = cn.GetString() ?? "全体殖民者";

            var action = drafted ? "已征召" : "已解除征召";
            var note = drafted ? "殖民者将中断当前工作并进入战斗状态。" : "殖民者将恢复日常工作。";
            return Task.FromResult(ToolResult.Success(colonist == "全体殖民者" ? $"全体 {action}。{note}" : $"{colonist} {action}。{note}"));
        }
    }
}
