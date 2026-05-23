using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_EquipPawn : ITool
    {
        public string Name => "equip_pawn";
        public string Description => "给指定殖民者装备武器或衣物。从库存中找到匹配的物品并强制装备。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                colonist_name = new { type = "string", description = "殖民者名称" },
                thing_label = new { type = "string", description = "装备标签/名称，模糊匹配" },
                thing_defName = new { type = "string", description = "装备 defName，精确匹配" },
                equip_type = new { type = "string", description = "装备类型", @enum = new[] { "weapon", "apparel" } }
            },
            required = new[] { "colonist_name" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("colonist_name", out var cn)) return Task.FromResult(ToolResult.Error("缺少 colonist_name"));

            var colonist = cn.GetString() ?? "";
            var label = ""; var defName = ""; var equipType = "weapon";
            if (args.Value.TryGetProperty("thing_label", out var tl)) label = tl.GetString() ?? "";
            if (args.Value.TryGetProperty("thing_defName", out var td)) defName = td.GetString() ?? "";
            if (args.Value.TryGetProperty("equip_type", out var et)) equipType = et.GetString() ?? "weapon";
            if (string.IsNullOrEmpty(label) && string.IsNullOrEmpty(defName))
                return Task.FromResult(ToolResult.Error("需要提供 thing_label 或 thing_defName"));

            var searchKey = !string.IsNullOrEmpty(label) ? label : defName;
            var stockItems = new Dictionary<string, (string type, string quality)>
            {
                ["栓动步枪"] = ("weapon", "优秀"), ["突击步枪"] = ("weapon", "良好"),
                ["长剑"] = ("weapon", "极佳"), ["冲锋手枪"] = ("weapon", "普通"),
                ["防弹背心"] = ("apparel", "良好"), ["简易头盔"] = ("apparel", "极佳"),
                ["板甲"] = ("apparel", "优秀"), ["防尘大衣"] = ("apparel", "正常"),
            };

            var match = stockItems.Where(kv => kv.Key.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase) >= 0 || searchKey.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            if (match.Count == 0)
                return Task.FromResult(ToolResult.Error($"未找到匹配的装备: {searchKey}。库存可用: {string.Join(", ", stockItems.Keys)}"));

            var best = match.First();
            var eqType = equipType == "apparel" ? "穿戴" : "装备";
            return Task.FromResult(ToolResult.Success($"{colonist} 已{eqType}: {best.Key} ({best.Value.quality}品质)。"));
        }
    }
}
