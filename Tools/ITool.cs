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
        /// <summary>从大模型参数中提取目标区域 (minX,minZ,maxX,maxZ)，返回 null 表示无需移动视角</summary>
        (int minX, int minZ, int maxX, int maxZ)? GetTargetRange(JsonElement? args);
    }

    public class ToolResult
    {
        public string Text { get; set; } = "";
        public bool IsError { get; set; }

        public static ToolResult Success(string text) => new() { Text = text };
        public static ToolResult Error(string text) => new() { Text = text, IsError = true };
    }
}
