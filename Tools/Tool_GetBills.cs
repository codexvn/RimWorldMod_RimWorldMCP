using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using RimWorldMCP;

namespace RimWorldMCP.Tools
{
    public class Tool_GetBills : ITool
    {
        public string Name => "get_bills";
        public string Description => "查看当前所有制造工作单的状态。返回中每个工作单前的方括号数字是 bill_index，供 manage_bill 使用。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { workbench_filter = new { type = "string", description = "按工作台类型 defName 或名称过滤" } }
        });

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var filter = "";
            if (args != null && args.Value.TryGetProperty("workbench_filter", out var f))
                filter = f.GetString() ?? "";

            return await McpCommandQueue.DispatchAsync(() =>
            {
                var map = Find.CurrentMap;
                if (map == null)
                    return ToolResult.Error("当前没有可用地图。");

                var tables = map.listerBuildings.AllBuildingsColonistOfClass<Building_WorkTable>().ToList();
                if (tables.Count == 0)
                    return ToolResult.Success("当前殖民地没有任何工作台。");

                var sb = new StringBuilder();
                sb.AppendLine("## 当前工作单");
                sb.AppendLine();

                var billIndex = 0;
                var totalBills = 0;
                var hasAnyMatch = false;

                foreach (var table in tables)
                {
                    var tableLabel = table.def?.label ?? table.def?.defName ?? "???";
                    var tableDefName = table.def?.defName ?? "???";

                    var bills = table.billStack?.Bills;
                    if (bills == null || bills.Count == 0)
                        continue;

                    // 按工作台过滤
                    if (!string.IsNullOrEmpty(filter))
                    {
                        if (tableLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0 &&
                            tableDefName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                    }

                    hasAnyMatch = true;
                    sb.AppendLine($"### {tableLabel} (`{tableDefName}`)");
                    sb.AppendLine();
                    sb.AppendLine("| 索引 | 单据 | 模式 | 详情 | 状态 |");
                    sb.AppendLine("|------|------|------|------|------|");

                    for (int i = 0; i < bills.Count; i++)
                    {
                        var bill = bills[i];
                        var label = bill.Label ?? "???";
                        var suspended = bill.suspended;
                        var statusText = suspended ? "暂停" : "运行中";

                        var modeText = "-";
                        var detailText = "-";

                        if (bill is Bill_Production prod)
                        {
                            if (prod.repeatMode == BillRepeatModeDefOf.Forever)
                            {
                                modeText = "永久重复";
                            }
                            else if (prod.repeatMode == BillRepeatModeDefOf.RepeatCount)
                            {
                                modeText = "重复指定次数";
                                var target = prod.targetCount;
                                var current = prod.repeatCount;
                                detailText = $"目标 {target} 次, 已完成 {current} 次";
                            }
                            else if (prod.repeatMode == BillRepeatModeDefOf.TargetCount)
                            {
                                modeText = "目标数量";
                                var target = prod.targetCount;
                                detailText = $"目标 {target} 件";
                            }
                            else
                            {
                                modeText = prod.repeatMode?.label ?? "单次";
                            }

                            if (prod.paused)
                                statusText = "已暂停(手动)";
                        }

                        sb.AppendLine($"| [{billIndex}] | {label} | {modeText} | {detailText} | {statusText} |");
                        billIndex++;
                        totalBills++;
                    }

                    sb.AppendLine();
                }

                if (!hasAnyMatch)
                {
                    if (!string.IsNullOrEmpty(filter))
                        return ToolResult.Success($"没有匹配过滤条件 \"{filter}\" 的工作单。");
                    else
                        return ToolResult.Success("当前没有工作单。");
                }

                sb.AppendLine($"**统计**: 共 {totalBills} 个工作单, 分布在工作台。");

                return ToolResult.Success(sb.ToString());
            });
        }
        public (int x, int y)? GetTargetPos(JsonElement? args) => null;
    }
}
