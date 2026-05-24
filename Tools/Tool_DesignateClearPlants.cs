using System;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using RimWorldMCP;

namespace RimWorldMCP.Tools
{
    public class Tool_DesignateClearPlants : ITool
    {
        public string Name => "designate_clear_plants";
        public string Description => "标记指定区域的杂草、灌木等非树木植物进行清除（CutPlant 命令）。不处理树木（树木请使用 designate_plants_cut）。提供 end_x/end_y 可划定矩形范围。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                pos_x = new { type = "integer", description = "起点 X 坐标（水平）" },
                pos_y = new { type = "integer", description = "起点 Y 坐标（垂直）" },
                end_x = new { type = "integer", description = "终点 X 坐标（可选，与 end_y 配对划定矩形范围）" },
                end_y = new { type = "integer", description = "终点 Y 坐标（可选，与 end_x 配对划定矩形范围）" },
                plant_defName = new { type = "string", description = "植物 defName 过滤（可选，只清除特定种类）" }
            },
            required = new[] { "pos_x", "pos_y" }
        });

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return ToolResult.Error("缺少参数");
            if (!args.Value.TryGetProperty("pos_x", out var jX) || !jX.TryGetInt32(out var posX))
                return ToolResult.Error("缺少必填参数: pos_x");
            if (!args.Value.TryGetProperty("pos_y", out var jY) || !jY.TryGetInt32(out var posY))
                return ToolResult.Error("缺少必填参数: pos_y");

            int endX = posX, endY = posY;
            bool isRange = args.Value.TryGetProperty("end_x", out var jEx) && jEx.TryGetInt32(out endX)
                        && args.Value.TryGetProperty("end_y", out var jEy) && jEy.TryGetInt32(out endY);

            string plantDefName = "";
            if (args.Value.TryGetProperty("plant_defName", out var jPlant))
                plantDefName = jPlant.GetString() ?? "";

            return await McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    Map map = Find.CurrentMap;
                    if (map == null) return ToolResult.Error("没有当前地图，请先加载游戏存档。");

                    int minX = Math.Min(posX, endX);
                    int maxX = Math.Max(posX, endX);
                    int minZ = Math.Min(posY, endY);
                    int maxZ = Math.Max(posY, endY);

                    CellRect area = CellRect.FromLimits(minX, minZ, maxX, maxZ);
                    area.ClipInsideMap(map);

                    if (area.IsEmpty)
                        return ToolResult.Error($"指定范围 ({minX},{minZ})~({maxX},{maxZ}) 完全在地图外。");

                    var cutDesignator = new Designator_PlantsCut();
                    int designated = 0, skipped = 0, filtered = 0;

                    foreach (IntVec3 cell in area)
                    {
                        if (cell.Fogged(map)) { skipped++; continue; }

                        Plant plant = cell.GetPlant(map);

                        if (!string.IsNullOrEmpty(plantDefName))
                        {
                            if (plant == null) { filtered++; continue; }
                            bool match = plant.def.defName.Equals(plantDefName, StringComparison.OrdinalIgnoreCase)
                                      || (plant.def.label != null && plant.def.label.IndexOf(plantDefName, StringComparison.OrdinalIgnoreCase) >= 0);
                            if (!match) { filtered++; continue; }
                        }

                        if (plant == null) { skipped++; continue; }

                        // 跳过树木，树木归 designate_plants_cut 处理
                        if (plant.def.plant.IsTree) { skipped++; continue; }

                        if (!cutDesignator.CanDesignateCell(cell).Accepted) { skipped++; continue; }
                        cutDesignator.DesignateSingleCell(cell);
                        designated++;
                    }

                    var sb = new StringBuilder();
                    string filterInfo = !string.IsNullOrEmpty(plantDefName) ? $"（过滤: {plantDefName}）" : "";
                    sb.Append(isRange
                        ? $"已标记清除范围 ({minX},{minZ})~({maxX},{maxZ}){filterInfo}：{designated} 株"
                        : $"已标记清除坐标 ({posX}, {posY}){filterInfo}：{designated} 株");
                    sb.Append($"。（跳过 {skipped}，不匹配过滤 {filtered}）");

                    return ToolResult.Success(sb.ToString());
                }
                catch (Exception ex) { return ToolResult.Error($"标记清除植物失败: {ex.Message}"); }
            });
        }
    }
}
