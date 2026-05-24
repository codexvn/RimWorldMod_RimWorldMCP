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
    public class Tool_GetTileGrid : ITool
    {
        public string Name => "get_tile_grid";
        public string Description => "获取指定范围的文本化网格地图。返回字符网格，用不同符号标注地形、建筑、物品。用于 LLM 理解地图空间布局。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                min_x = new { type = "integer", description = "网格 X 范围最小值（水平轴）" },
                min_y = new { type = "integer", description = "网格 Y 范围最小值（垂直轴，即 IntVec3.z）" },
                max_x = new { type = "integer", description = "网格 X 范围最大值" },
                max_y = new { type = "integer", description = "网格 Y 范围最大值" }
            },
            required = new[] { "min_x", "min_y", "max_x", "max_y" }
        });

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return ToolResult.Error("缺少参数");
            if (!args.Value.TryGetProperty("min_x", out var jMinX) || !jMinX.TryGetInt32(out var minX))
                return ToolResult.Error("缺少 min_x");
            if (!args.Value.TryGetProperty("min_y", out var jMinY) || !jMinY.TryGetInt32(out var minY))
                return ToolResult.Error("缺少 min_y");
            if (!args.Value.TryGetProperty("max_x", out var jMaxX) || !jMaxX.TryGetInt32(out var maxX))
                return ToolResult.Error("缺少 max_x");
            if (!args.Value.TryGetProperty("max_y", out var jMaxY) || !jMaxY.TryGetInt32(out var maxY))
                return ToolResult.Error("缺少 max_y");

            if (maxX - minX > 80 || maxY - minY > 80)
                return ToolResult.Error("网格范围不能超过 80x80");

            return await McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return ToolResult.Error("当前没有可用地图。");

                    int mapW = map.Size.x, mapH = map.Size.z;
                    if (minX < 0 || minY < 0 || maxX >= mapW || maxY >= mapH)
                        return ToolResult.Error($"坐标超出地图边界 (0~{mapW - 1}, 0~{mapH - 1})");

                    int w = maxX - minX + 1;
                    int h = maxY - minY + 1;

                    var grid = new char[h][];
                    for (int i = 0; i < h; i++) grid[i] = new char[w];

                    for (int gy = minY; gy <= maxY; gy++)
                    {
                        for (int gx = minX; gx <= maxX; gx++)
                        {
                            var pos = new IntVec3(gx, 0, gy);
                            int row = gy - minY;
                            int col = gx - minX;

                            // Buildings
                            var b = pos.GetEdifice(map);
                            if (b != null)
                            {
                                if (b.def.altitudeLayer == AltitudeLayer.DoorMoveable)
                                    grid[row][col] = 'D'; // door
                                else
                                    grid[row][col] = b.def.altitudeLayer >= AltitudeLayer.Building ? 'B' : '#';
                                continue;
                            }

                            // Blueprints / Frames / Items at this cell
                            var things = pos.GetThingList(map);
                            var bp = things.FirstOrDefault(t => t is Blueprint || t is Frame);
                            if (bp != null)
                            {
                                grid[row][col] = '∎';
                                continue;
                            }

                            var item = things.FirstOrDefault(t => t.def.category == ThingCategory.Item);
                            if (item != null)
                            {
                                grid[row][col] = '○';
                                continue;
                            }

                            // Plants
                            var plant = pos.GetPlant(map);
                            if (plant != null)
                            {
                                grid[row][col] = plant.def.plant.IsTree ? '♣' : ';';
                                continue;
                            }

                            // Zones
                            var zone = map.zoneManager?.ZoneAt(pos);
                            if (zone is Zone_Growing)
                                grid[row][col] = '=';
                            else if (zone is Zone_Stockpile)
                                grid[row][col] = 'S';
                            else
                                goto terrain;

                            continue;

                            terrain:
                            // Terrain
                            var terrain = map.terrainGrid.TerrainAt(pos);
                            if (terrain != null)
                            {
                                grid[row][col] = terrain.defName.Contains("Water") || terrain.defName.Contains("Marsh") ? '~'
                                    : terrain.defName.Contains("Mud") ? '≈'
                                    : terrain.defName.Contains("Sand") ? '·'
                                    : terrain.defName.Contains("Soil") || terrain.defName.Contains("Rich") ? '.'
                                    : terrain.defName.Contains("Gravel") ? ','
                                    : '.';
                            }
                            else
                            {
                                grid[row][col] = '?';
                            }
                        }
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"## 网格 ({minX},{minY}) ~ ({maxX},{maxY})  [{w}x{h}]");
                    sb.AppendLine();
                    for (int row = 0; row < h; row++)
                    {
                        for (int col = 0; col < w; col++)
                            sb.Append(grid[row][col]);
                        sb.AppendLine();
                    }
                    sb.AppendLine();
                    sb.AppendLine("图例: #=墙 B=建筑 D=门 ∎=蓝图 ○=物品 ;=作物 ♣=树 ==种植区 S=储存区 ~=水 ≈=泥 ·=沙 .=土 ,=砾石");

                    return ToolResult.Success(sb.ToString().TrimEnd());
                }
                catch (Exception ex)
                {
                    return ToolResult.Error($"网格生成失败: {ex.Message}");
                }
            });
        }
    }
}
