using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetColonists : ITool
    {
        public string Name => "get_colonists";
        public string Description => "获取所有殖民者的详细信息，包括技能等级、心情、健康状态、当前装备和工作任务。可按名称过滤。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { colonist_name = new { type = "string", description = "殖民者名称（模糊匹配），不传则返回全部" } }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var nameFilter = "";
            if (args != null && args.Value.TryGetProperty("colonist_name", out var n)) nameFilter = n.GetString() ?? "";

            var colonists = new[]
            {
                new { name="王建国", age="34岁男", mood="78% (满意)", skills="建造12* | 采矿8 | 射击10**", health="健康", equipment="栓动步枪(优秀)", work="建造屋顶", workPrio="建造1 采矿2 搬运3" },
                new { name="李秀英", age="28岁女", mood="85% (愉快)", skills="烹饪14** | 缝纫10* | 种植7", health="健康", equipment="无", work="烹饪简单食物", workPrio="烹饪1 缝纫2 种植3" },
                new { name="张铁柱", age="42岁男", mood="65% (一般)", skills="锻造16** | 手工12* | 采矿10", health="右手旧伤(效率-10%)", equipment="长剑(极佳)", work="锻造长剑", workPrio="锻造1 手工2 采矿3" },
                new { name="陈美玲", age="23岁女", mood="90% (非常愉快)", skills="种植11* | 驯兽8 | 医疗7", health="左手轻微擦伤", equipment="无", work="收割稻米", workPrio="种植1 驯兽2 医疗3" },
                new { name="赵大力", age="31岁男", mood="72% (满意)", skills="射击14** | 格斗10* | 采矿6", health="健康", equipment="突击步枪(良好)+防弹背心", work="巡逻", workPrio="射击1 格斗2 采矿3" },
                new { name="刘小芳", age="19岁女", mood="55% (低落)", skills="研究10* | 医疗9* | 社交7", health="轻度食物中毒", equipment="无", work="研究微型电子学", workPrio="研究1 医疗2 社交3" },
            };

            var filtered = colonists.AsEnumerable();
            if (!string.IsNullOrEmpty(nameFilter))
                filtered = filtered.Where(c => c.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            var lines = filtered.Select(c => $"- {c.name} ({c.age}) | 心情: {c.mood} | 健康: {c.health}\n  技能: {c.skills}\n  装备: {c.equipment} | 当前: {c.work}\n  工作优先级: {c.workPrio}").ToList();
            var result = lines.Count > 0 ? $"殖民者 ({lines.Count} 人):\n\n" + string.Join("\n\n", lines) : "没有匹配的殖民者。";
            return Task.FromResult(ToolResult.Success(result));
        }
    }
}
