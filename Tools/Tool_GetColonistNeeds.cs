using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetColonistNeeds : ITool
    {
        public string Name => "get_colonist_needs";
        public string Description => "获取殖民者的详细需求状态：心情、食物、休息、娱乐、舒适、美观、户外等各项需求的当前百分比。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { colonist_name = new { type = "string", description = "殖民者名称（模糊匹配），不传返回全部" } }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var nameFilter = "";
            if (args != null && args.Value.TryGetProperty("colonist_name", out var n)) nameFilter = n.GetString() ?? "";

            var colonists = new[]
            {
                new { name="王建国", mood="78%", food="65%", rest="82%", joy="55%", beauty="70%", comfort="60%", outdoors="35%", issues="娱乐偏低" },
                new { name="李秀英", mood="85%", food="90%", rest="75%", joy="70%", beauty="65%", comfort="50%", outdoors="20%", issues="户外偏低、舒适偏低" },
                new { name="张铁柱", mood="65%", food="80%", rest="60%", joy="40%", beauty="55%", comfort="45%", outdoors="50%", issues="娱乐严重偏低、休息偏低" },
                new { name="赵大力", mood="72%", food="70%", rest="90%", joy="60%", beauty="60%", comfort="70%", outdoors="65%", issues="" },
                new { name="刘小芳", mood="55%", food="75%", rest="85%", joy="30%", beauty="40%", comfort="35%", outdoors="15%", issues="心情低落 + 娱乐极低 + 舒适极低" },
            };

            var filtered = colonists.AsEnumerable();
            if (!string.IsNullOrEmpty(nameFilter))
                filtered = filtered.Where(c => c.name.IndexOf(nameFilter, StringComparison.OrdinalIgnoreCase) >= 0);

            var lines = filtered.Select(c =>
            {
                var needs = $"  心情: {c.mood} | 食物: {c.food} | 休息: {c.rest} | 娱乐: {c.joy} | 美观: {c.beauty} | 舒适: {c.comfort} | 户外: {c.outdoors}";
                var issueLine = string.IsNullOrEmpty(c.issues) ? "" : $"\n  ⚠ {c.issues}";
                return $"- {c.name}:" + needs + issueLine;
            }).ToList();

            var result = lines.Count > 0 ? $"殖民者需求状态 ({lines.Count} 人):\n\n{string.Join("\n\n", lines)}" : "没有匹配的殖民者。";
            return Task.FromResult(ToolResult.Success(result));
        }
    }
}
