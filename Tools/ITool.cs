using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        JsonElement InputSchema { get; }
        Task<ToolResult> ExecuteAsync(JsonElement? args);
    }

    public class ToolResult
    {
        public string Text { get; set; } = "";
        public bool IsError { get; set; }

        public static ToolResult Success(string text) => new() { Text = text };
        public static ToolResult Error(string text) => new() { Text = text, IsError = true };
    }
}
