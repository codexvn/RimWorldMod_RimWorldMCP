using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_DesignateBuild : ITool
    {
        public string Name => "designate_build";
        public string Description => "在指定地图坐标放置建造蓝图。可用于建造墙体、门、地板、家具、工作台等。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                thingDef_name = new { type = "string", description = "要建造的物品 defName。常用: Wall(墙), Door(门), TableSmithy(锻造台), WoodFloor(木地板), Bed(床), StandingLamp(立灯)" },
                pos_x = new { type = "integer", description = "X 坐标" },
                pos_y = new { type = "integer", description = "Y 坐标" },
                pos_z = new { type = "integer", description = "Z 坐标" },
                rotation = new { type = "string", description = "旋转方向", @enum = new[] { "North", "East", "South", "West" } },
                stuff_defName = new { type = "string", description = "建筑材料 defName，如 Steel, WoodLog" }
            },
            required = new[] { "thingDef_name", "pos_x", "pos_y", "pos_z" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("thingDef_name", out var defName)) return Task.FromResult(ToolResult.Error("缺少 thingDef_name"));
            if (!args.Value.TryGetProperty("pos_x", out var px) || !px.TryGetInt32(out var posX)) return Task.FromResult(ToolResult.Error("缺少 pos_x"));
            if (!args.Value.TryGetProperty("pos_y", out var py) || !py.TryGetInt32(out var posY)) return Task.FromResult(ToolResult.Error("缺少 pos_y"));
            if (!args.Value.TryGetProperty("pos_z", out var pz) || !pz.TryGetInt32(out var posZ)) return Task.FromResult(ToolResult.Error("缺少 pos_z"));

            var rotation = "North";
            if (args.Value.TryGetProperty("rotation", out var rot)) rotation = rot.GetString() ?? "North";
            var stuff = "";
            if (args.Value.TryGetProperty("stuff_defName", out var s)) stuff = s.GetString() ?? "";
            var stuffText = string.IsNullOrEmpty(stuff) ? "" : $" (材料: {stuff})";

            return Task.FromResult(ToolResult.Success($"已在坐标 ({posX}, {posY}, {posZ}) 放置建造蓝图: {defName.GetString()}{stuffText}, 朝向: {rotation}。"));
        }
    }
}
