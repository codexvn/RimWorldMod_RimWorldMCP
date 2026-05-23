using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetColonistHealth : ITool
    {
        public string Name => "get_colonist_health";
        public string Description => "获取殖民者的详细健康报告。包括伤势、疾病、身体部位状态、手术需求等。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { colonist_name = new { type = "string", description = "殖民者名称（模糊匹配），不传返回全部" } }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var nameFilter = "";
            if (args != null && args.Value.TryGetProperty("colonist_name", out var n)) nameFilter = n.GetString() ?? "";

            var colonists = new (string name, string health)[]
            {
                ("王建国", "健康 | 无异常"),
                ("李秀英", "健康 | 无异常"),
                ("张铁柱", "⚠ 右手旧伤（永久，操作能力-10%）| 手术建议: 仿生手臂安装"),
                ("赵大力", "健康 | 无异常"),
                ("刘小芳", "⚠ 轻度食物中毒（剩余 0.3 天）| 建议: 卧床休息"),
                ("陈美玲", "⚠ 左手轻微擦伤（已包扎，3 天后痊愈）| 无大碍"),
            };

            var filtered = colonists.AsEnumerable();
            if (!string.IsNullOrEmpty(nameFilter))
                filtered = filtered.Where(c => c.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            var lines = filtered.Select(c => $"- **{c.name}**: {c.health}").ToList();
            var result = lines.Count > 0 ? $"殖民者健康报告 ({lines.Count} 人):\n\n{string.Join("\n\n", lines)}" : "没有匹配的殖民者。";
            return Task.FromResult(ToolResult.Success(result));
        }
    }
}
