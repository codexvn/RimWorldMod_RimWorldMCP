using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace RimWorldMCP.Tools
{
    public class Tool_GetTerrainInfo : ITool
    {
        public string Name => "get_terrain_info";
        public string Description => "获取指定区域的土壤信息：地形、肥沃度、温度、污染状态。用于评估种植/建造规划。小范围（≤25 格）逐格展示，大范围输出统计摘要。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                pos_x = new { type = "integer", description = "左上 X 坐标" },
                pos_y = new { type = "integer", description = "左上 Y 坐标" },
                end_x = new { type = "integer", description = "右下 X 坐标（可选，不传只查单格）" },
                end_y = new { type = "integer", description = "右下 Y 坐标（可选，不传只查单格）" }
            },
            required = new[] { "pos_x", "pos_y" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("pos_x", out var jSx) || !jSx.TryGetInt32(out var posX))
                return Task.FromResult(ToolResult.Error("缺少必填参数: pos_x"));
            if (!args.Value.TryGetProperty("pos_y", out var jSy) || !jSy.TryGetInt32(out var posY))
                return Task.FromResult(ToolResult.Error("缺少必填参数: pos_y"));

            int endX = posX, endY = posY;
            bool hasEnd = false;
            if (args.Value.TryGetProperty("end_x", out var jEx) && jEx.TryGetInt32(out var ex)
                && args.Value.TryGetProperty("end_y", out var jEy) && jEy.TryGetInt32(out var ey))
            {
                endX = ex; endY = ey; hasEnd = true;
            }

            int minX = Math.Min(posX, endX), maxX = Math.Max(posX, endX);
            int minZ = Math.Min(posY, endY), maxZ = Math.Max(posY, endY);
            int totalCells = (maxX - minX + 1) * (maxZ - minZ + 1);

            var capMinX = minX; var capMaxX = maxX; var capMinZ = minZ; var capMaxZ = maxZ;
            var capHasEnd = hasEnd; var capTotal = totalCells;

            return McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    Map map = Find.CurrentMap;
                    if (map == null) return ToolResult.Error("没有当前地图");

                    if (minX < 0 || minZ < 0 || maxX >= map.Size.x || maxZ >= map.Size.z)
                        return ToolResult.Error($"范围 ({minX},{minZ})~({maxX},{maxZ}) 超出地图边界");

                    // 逐格收集数据
                    var cells = new List<TerrainCellInfo>();
                    float sumFertility = 0, sumTemp = 0;
                    int pollutedCount = 0;
                    float minFert = float.MaxValue, maxFert = float.MinValue;
                    float minTemp = float.MaxValue, maxTemp = float.MinValue;

                    for (int x = minX; x <= maxX; x++)
                    {
                        for (int z = minZ; z <= maxZ; z++)
                        {
                            var cell = new IntVec3(x, 0, z);
                            var terrain = map.terrainGrid.TerrainAt(cell);
                            float fertility = terrain?.fertility ?? 0f;
                            float temp = GenTemperature.GetTemperatureForCell(cell, map);
                            bool polluted = ModsConfig.BiotechActive && map.pollutionGrid.IsPolluted(cell);

                            cells.Add(new TerrainCellInfo
                            {
                                X = x, Z = z,
                                Terrain = terrain?.label ?? "?",
                                Fertility = fertility,
                                Temperature = temp,
                                Polluted = polluted
                            });

                            sumFertility += fertility;
                            sumTemp += temp;
                            if (polluted) pollutedCount++;
                            if (fertility < minFert) minFert = fertility;
                            if (fertility > maxFert) maxFert = fertility;
                            if (temp < minTemp) minTemp = temp;
                            if (temp > maxTemp) maxTemp = temp;
                        }
                    }

                    var sb = new StringBuilder();
                    sb.Append($"范围 ({minX},{minZ})");
                    if (capHasEnd) sb.Append($"~({maxX},{maxZ})");
                    sb.AppendLine($" | {capTotal} 格");

                    // 大范围 → 摘要
                    if (capTotal > 25)
                    {
                        sb.AppendLine($"- 地形种类: {cells.GroupBy(c => c.Terrain).Count()} 种");
                        var topTerrains = cells.GroupBy(c => c.Terrain)
                            .OrderByDescending(g => g.Count())
                            .Take(5);
                        foreach (var g in topTerrains)
                            sb.AppendLine($"  - {g.Key}: {g.Count()} 格");
                        sb.AppendLine($"- 肥沃度: {minFert:F0}%~{maxFert:F0}% (平均 {sumFertility / capTotal:F0}%)");
                        sb.AppendLine($"- 温度: {minTemp:F0}°C~{maxTemp:F0}°C (平均 {sumTemp / capTotal:F0}°C)");
                        if (pollutedCount > 0)
                            sb.AppendLine($"- 污染: {pollutedCount}/{capTotal} 格 ({100 * pollutedCount / capTotal:F0}%)");
                        else
                            sb.AppendLine("- 污染: 无");
                        // 适合种植的土壤
                        var growable = cells.Where(c => c.Fertility >= 100).ToList();
                        if (growable.Count > 0)
                            sb.AppendLine($"- 可种植土壤（肥沃度≥100%）: {growable.Count} 格");
                        return ToolResult.Success(sb.ToString().TrimEnd());
                    }

                    // 小范围 → 逐格展示
                    var headParts = new List<string> { "坐标", "地形", "肥%", "温度", "污染" };
                    sb.AppendLine(string.Join(" | ", headParts));
                    sb.AppendLine(new string('-', headParts.Count * 8));

                    foreach (var c in cells)
                    {
                        var parts = new List<string>
                        {
                            $"({c.X},{c.Z})".PadRight(8),
                            c.Terrain.PadRight(10),
                            $"{c.Fertility:F0}%".PadRight(6),
                            $"{c.Temperature:F0}°C".PadRight(6),
                            c.Polluted ? "是" : "否"
                        };
                        sb.AppendLine(string.Join(" | ", parts));
                    }

                    // 小范围摘要
                    sb.AppendLine(new string('-', headParts.Count * 8));
                    sb.AppendLine($"肥沃度: {minFert:F0}%~{maxFert:F0}% | 温度: {minTemp:F0}°C~{maxTemp:F0}°C | 污染: {pollutedCount}/{capTotal}");

                    return ToolResult.Success(sb.ToString().TrimEnd());
                }
                catch (Exception ex) { return ToolResult.Error($"地形查询失败: {ex.Message}"); }
            });
        }

        public (int x, int y)? GetTargetPos(JsonElement? args)
        {
            if (args == null) return null;
            if (!args.Value.TryGetProperty("pos_x", out var jX) || !jX.TryGetInt32(out var posX)) return null;
            if (!args.Value.TryGetProperty("pos_y", out var jY) || !jY.TryGetInt32(out var posY)) return null;
            return (posX, posY);
        }

        private struct TerrainCellInfo
        {
            public int X, Z;
            public string Terrain;
            public float Fertility;
            public float Temperature;
            public bool Polluted;
        }
    }
}
