using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetBills : ITool
    {
        public string Name => "get_bills";
        public string Description => "查看当前所有制造工作单的状态。返回中每个工作单前的方括号数字是 bill_index，供 manage_bill 使用。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { workbench_filter = new { type = "string", description = "按工作台类型过滤" } }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var filter = "";
            if (args != null && args.Value.TryGetProperty("workbench_filter", out var f)) filter = f.GetString() ?? "";

            var bills = new[]
            {
                new { workbench="裁缝台", item="高级衬衫", mode="目标数量", info="2/3 已完成 | 进行中", paused=false },
                new { workbench="裁缝台", item="防弹夹克", mode="重复2次", info="剩余 2 次 | 暂停", paused=true },
                new { workbench="锻造台", item="长剑", mode="重复1次", info="剩余 1 次 | 进行中", paused=false },
                new { workbench="锻造台", item="板甲", mode="重复2次", info="剩余 2 次 | 暂停", paused=true },
                new { workbench="炉灶", item="简单食物", mode="永久", info="进行中", paused=false },
            };

            var filtered = bills.AsEnumerable();
            if (!string.IsNullOrEmpty(filter))
                filtered = filtered.Where(b => b.workbench.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0);

            var lines = filtered.Select((b, i) => $"  [{i}] {b.workbench}: {b.item} | {b.mode} | {b.info} {(b.paused ? "暂停" : "运行中")}").ToList();
            var result = lines.Count > 0 ? $"当前工作单 ({lines.Count} 个):\n" + string.Join("\n", lines) : "当前没有匹配的工作单。";
            return Task.FromResult(ToolResult.Success(result));
        }
    }
}
