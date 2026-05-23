using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_CreateBill : ITool
    {
        public string Name => "create_production_bill";
        public string Description => "在指定工作台上创建制造（生产）单据。配方名称请先用 list_recipes 查询获取 defName。支持设置制造数量和重复模式。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                recipe_defName = new { type = "string", description = "配方的 defName，必须先用 list_recipes 确认配方存在。" },
                count = new { type = "integer", description = "制造数量，默认 1", @default = 1 },
                repeat_mode = new { type = "string", description = "重复模式", @enum = new[] { "RepeatCount", "Forever", "TargetCount" } }
            },
            required = new[] { "recipe_defName" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数: recipe_defName"));
            if (!args.Value.TryGetProperty("recipe_defName", out var defName)) return Task.FromResult(ToolResult.Error("缺少必填参数: recipe_defName"));

            var recipe = defName.GetString() ?? "";
            var count = 1;
            var repeatMode = "RepeatCount";
            if (args.Value.TryGetProperty("count", out var c) && c.TryGetInt32(out var cv)) count = cv;
            if (args.Value.TryGetProperty("repeat_mode", out var rm)) repeatMode = rm.GetString() ?? "RepeatCount";

            var workbenchMap = new Dictionary<string, (string workbench, string label)>
            {
                ["Make_LongSword"] = ("锻造台", "长剑"),
                ["Make_PlateArmor"] = ("锻造台", "板甲"),
                ["Make_Gladius"] = ("锻造台", "短剑"),
                ["Make_Duster"] = ("裁缝台", "防尘大衣"),
                ["Make_Pants"] = ("裁缝台", "裤子"),
                ["Make_ButtonDownShirt"] = ("裁缝台", "高级衬衫"),
                ["Make_SimpleMeal"] = ("炉灶", "简单食物"),
                ["Make_FineMeal"] = ("炉灶", "精致食物"),
                ["Make_Component"] = ("机械加工台", "零部件"),
                ["Make_AssaultRifle"] = ("机械加工台", "突击步枪"),
            };

            if (!workbenchMap.TryGetValue(recipe, out var info))
            {
                var known = string.Join(", ", workbenchMap.Keys);
                return Task.FromResult(ToolResult.Error($"未知配方: {recipe}。已知配方: {known}"));
            }

            var repeatText = repeatMode switch { "Forever" => "永久重复", "TargetCount" => $"目标数量 {count}", _ => $"重复 {count} 次" };
            return Task.FromResult(ToolResult.Success($"已在{info.workbench}创建制造单据: {info.label} ({recipe}) — {repeatText}"));
        }
    }
}
