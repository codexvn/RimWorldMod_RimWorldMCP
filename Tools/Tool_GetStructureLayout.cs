using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Verse;
using RimWorld;

namespace RimWorldMCP.Tools
{
    public class Tool_GetStructureLayout : ITool
    {
        public string Name => "get_structure_layout";
        public string Description => "获取地图建筑结构布局。无参数时输出全图。包含: 空间网格(墙/门/建筑), 房间列表(类型/面积/评分), 门列表, 墙段RLE。用于基地规划与建筑分析。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                pos_x = new { type = "integer", description = "查询区域左上角 X（可选，默认0）" },
                pos_y = new { type = "integer", description = "查询区域左上角 Y（可选，默认0）" },
                end_x = new { type = "integer", description = "查询区域右下角 X（可选，默认地图右边界）" },
                end_y = new { type = "integer", description = "查询区域右下角 Y（可选，默认地图下边界）" }
            }
        });

        private const int MaxGridWidth = 80;
        private const int MaxGridHeight = 60;
        private const int MaxRooms = 30;

        private static readonly Dictionary<char, string> LegendMap = new()
        {
            ['#'] = "玩家墙",
            ['R'] = "自然岩壁",
            ['+'] = "门",
            ['B'] = "其他建筑",
            ['.'] = "空地"
        };

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            return await McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return ToolResult.Error("当前没有可用地图。");

                    int mapW = map.Size.x;
                    int mapH = map.Size.z;

                    int minX = 0, minY = 0;
                    int maxX = mapW - 1, maxY = mapH - 1;

                    if (args != null)
                    {
                        if (args.Value.TryGetProperty("pos_x", out var jX) && jX.TryGetInt32(out var px))
                            minX = px;
                        if (args.Value.TryGetProperty("pos_y", out var jY) && jY.TryGetInt32(out var py))
                            minY = py;
                        if (args.Value.TryGetProperty("end_x", out var jEx) && jEx.TryGetInt32(out var ex))
                            maxX = ex;
                        if (args.Value.TryGetProperty("end_y", out var jEy) && jEy.TryGetInt32(out var ey))
                            maxY = ey;
                    }

                    if (minX > maxX) { int t = minX; minX = maxX; maxX = t; }
                    if (minY > maxY) { int t = minY; minY = maxY; maxY = t; }

                    minX = Math.Max(0, Math.Min(minX, mapW - 1));
                    maxX = Math.Max(0, Math.Min(maxX, mapW - 1));
                    minY = Math.Max(0, Math.Min(minY, mapH - 1));
                    maxY = Math.Max(0, Math.Min(maxY, mapH - 1));

                    int w = maxX - minX + 1;
                    int h = maxY - minY + 1;
                    bool showGrid = w <= MaxGridWidth && h <= MaxGridHeight;

                    var sb = new StringBuilder();
                    sb.AppendLine($"## 建筑结构布局 ({minX},{minY}) ~ ({maxX},{maxY})  [{w}x{h}]");

                    if (!showGrid)
                        sb.AppendLine($"> 全图模式：字符网格已省略（范围 {w}x{h} 超过 {MaxGridWidth}x{MaxGridHeight} 上限），使用墙段RLE+房间+门表达结构。");

                    sb.AppendLine();

                    // 1. 空间网格（仅小范围）
                    var usedSymbols = new HashSet<char>();
                    if (showGrid)
                    {
                        usedSymbols = BuildGrid(sb, minX, minY, maxX, maxY, map);
                        sb.AppendLine();
                    }

                    // 2. 房间
                    int roomCount = BuildRoomList(sb, minX, minY, maxX, maxY, map);
                    if (roomCount > 0) sb.AppendLine();

                    // 3. 门
                    int doorCount = BuildDoorList(sb, minX, minY, maxX, maxY, map);
                    if (doorCount > 0) sb.AppendLine();

                    // 4. 墙段 RLE
                    BuildWallRuns(sb, minX, minY, maxX, maxY, map);

                    // 5. 图例（仅小范围模式）
                    if (showGrid && usedSymbols.Count > 0)
                    {
                        sb.AppendLine();
                        BuildLegend(sb, usedSymbols);
                    }

                    return ToolResult.Success(sb.ToString().TrimEnd());
                }
                catch (Exception ex)
                {
                    return ToolResult.Error($"结构布局生成失败: {ex.Message}");
                }
            });
        }

        private static bool IsWallCell(Building ed, out bool isNatural)
        {
            isNatural = false;
            if (ed?.def == null) return false;

            var def = ed.def;

            if (def.IsDoor) return false;

            if (def.building != null && def.building.isNaturalRock)
            {
                isNatural = true;
                return true;
            }

            if (def.Fillage == FillCategory.Full)
                return true;

            if (def.building != null && def.building.isWall)
                return true;

            if (def.passability == Traversability.Impassable)
                return true;

            return false;
        }

        private static char GetGridChar(IntVec3 c, Map map)
        {
            var ed = c.GetEdifice(map);
            if (ed == null) return '.';

            if (ed is Building_Door) return '+';

            if (IsWallCell(ed, out bool isNatural))
                return isNatural ? 'R' : '#';

            return 'B';
        }

        private static HashSet<char> BuildGrid(StringBuilder sb, int minX, int minY, int maxX, int maxY, Map map)
        {
            var used = new HashSet<char>();
            int w = maxX - minX + 1;

            sb.AppendLine("### 空间网格");

            for (int z = minY; z <= maxY; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    char ch = GetGridChar(new IntVec3(x, 0, z), map);
                    sb.Append(ch);
                    used.Add(ch);
                }
                sb.AppendLine();
            }

            return used;
        }

        private static void BuildLegend(StringBuilder sb, HashSet<char> usedSymbols)
        {
            sb.Append("### 图例");
            sb.AppendLine();
            var parts = new List<string>();
            foreach (var ch in usedSymbols.OrderBy(c => c))
            {
                if (LegendMap.TryGetValue(ch, out var label))
                    parts.Add($"{ch}={label}");
            }
            if (parts.Count > 0)
                sb.AppendLine(string.Join("  ", parts));
        }

        private static int BuildRoomList(StringBuilder sb, int minX, int minY, int maxX, int maxY, Map map)
        {
            var regionGrid = map.regionGrid;
            if (regionGrid == null) return 0;

            var allRooms = regionGrid.AllRooms;
            if (allRooms == null || allRooms.Count == 0) return 0;

            var matchedRooms = new List<Room>();
            int outdoorCellCount = 0;

            foreach (var room in allRooms)
            {
                if (room == null || room.CellCount == 0) continue;

                // 室外房间先分流（免遍历巨量 Cells），合并统计
                if (room.TouchesMapEdge && room.UsesOutdoorTemperature)
                {
                    outdoorCellCount += room.CellCount;
                    continue;
                }

                // 仅对室内房间做精确范围交集检测
                if (!RoomIntersectsRange(room, minX, minY, maxX, maxY))
                    continue;

                matchedRooms.Add(room);
            }

            // 按面积降序排列，取前 MaxRooms
            matchedRooms.Sort((a, b) => b.CellCount.CompareTo(a.CellCount));
            int total = matchedRooms.Count;
            int shown = Math.Min(total, MaxRooms);
            matchedRooms = matchedRooms.Take(MaxRooms).ToList();

            sb.AppendLine($"### 房间 ({shown})");

            for (int i = 0; i < matchedRooms.Count; i++)
            {
                var room = matchedRooms[i];
                string role = room.Role?.label ?? "未分类";
                int area = room.CellCount;
                float impressiveness = room.GetStat(RoomStatDefOf.Impressiveness);
                int beds = room.ContainedBeds?.Count() ?? 0;
                string env = room.UsesOutdoorTemperature ? "室外" : "室内";

                // 取房间第一个Cell作为代表坐标
                var loc = room.Cells.FirstOrDefault();
                string coord = $"({loc.x},{loc.z})";

                sb.AppendLine($"[{i + 1}] {role}  面积={area}  印象={impressiveness:F1}  床位={beds}  {env}  {coord}");
            }

            // 室外区域
            if (outdoorCellCount > 0)
                sb.AppendLine($"[室外] 面积={outdoorCellCount}  室外区域");

            // 截断提示
            if (total > MaxRooms)
                sb.AppendLine($"> 另有 {total - MaxRooms} 个房间未列出（按面积截断）");

            return shown;
        }

        private static bool RoomIntersectsRange(Room room, int minX, int minY, int maxX, int maxY)
        {
            // 快速检测：用 ExtentsClose 边界框
            var ext = room.ExtentsClose;
            if (ext.maxX < minX || ext.minX > maxX || ext.maxZ < minY || ext.minZ > maxY)
                return false;

            // 精确检测：遍历 Cells（房间格数通常不大）
            foreach (var cell in room.Cells)
            {
                if (cell.x >= minX && cell.x <= maxX && cell.z >= minY && cell.z <= maxY)
                    return true;
            }

            return false;
        }

        private static int BuildDoorList(StringBuilder sb, int minX, int minY, int maxX, int maxY, Map map)
        {
            var doors = new List<Building_Door>();

            for (int z = minY; z <= maxY; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var door = new IntVec3(x, 0, z).GetDoor(map);
                    if (door != null && !doors.Contains(door))
                        doors.Add(door);
                }
            }

            if (doors.Count == 0) return 0;

            sb.AppendLine($"### 门 ({doors.Count})");

            for (int i = 0; i < doors.Count; i++)
            {
                var door = doors[i];
                float hpPct = (float)door.HitPoints / door.MaxHitPoints * 100f;
                string material = door.Stuff?.label ?? "默认材质";
                string status = door.Open ? "开启" : (door.HoldOpen ? "保持开启" : "关闭");
                sb.AppendLine($"[{i + 1}] ({door.Position.x},{door.Position.z}) {door.def.label}  {material}  {hpPct:F0}%  {status}");
            }

            return doors.Count;
        }

        private struct WallRun
        {
            public int Id;
            public int X1, Y, X2;
            public bool IsNatural;
        }

        private static int BuildWallRuns(StringBuilder sb, int minX, int minY, int maxX, int maxY, Map map)
        {
            var allRuns = new List<WallRun>();
            int globalId = 0;

            for (int z = minY; z <= maxY; z++)
            {
                int x = minX;
                while (x <= maxX)
                {
                    var ed = new IntVec3(x, 0, z).GetEdifice(map);
                    if (ed != null && IsWallCell(ed, out bool isNatural))
                    {
                        int startX = x;
                        // 延伸同类型墙
                        while (x + 1 <= maxX)
                        {
                            var nextEd = new IntVec3(x + 1, 0, z).GetEdifice(map);
                            if (nextEd == null || !IsWallCell(nextEd, out bool nextNatural) || nextNatural != isNatural)
                                break;
                            x++;
                        }
                        allRuns.Add(new WallRun { Id = ++globalId, X1 = startX, Y = z, X2 = x, IsNatural = isNatural });
                    }
                    x++;
                }
            }

            sb.AppendLine($"### 墙段 ({globalId})");

            if (globalId == 0) return 0;

            // 按行分组输出
            var runsByRow = allRuns.GroupBy(r => r.Y).OrderBy(g => g.Key);
            foreach (var group in runsByRow)
            {
                sb.Append($"行{group.Key}:");
                foreach (var run in group)
                {
                    string prefix = run.IsNatural ? "R" : "";
                    sb.Append($" {prefix}W{run.Id}[({run.X1},{run.Y})→({run.X2},{run.Y}) len={run.X2 - run.X1 + 1}]");
                }
                sb.AppendLine();
            }

            return globalId;
        }
    }
}
