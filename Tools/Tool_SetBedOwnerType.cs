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
    public class Tool_SetBedOwnerType : ITool
    {
        public string Name => "set_bed_owner_type";
        public string Description => "设置床的归属类型（殖民者/俘虏）。切换为俘虏床时会检查是否导致殖民者无床可用，传 force=true 可跳过安全校验。";

        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                pos_x = new { type = "integer", description = "床的 X 坐标" },
                pos_y = new { type = "integer", description = "床的 Y 坐标" },
                owner_type = new { type = "string", description = "归属类型: colonist, prisoner, slave(仅Ideology)" },
                force = new { type = "boolean", description = "跳过安全校验强制切换（默认 false）", @default = false }
            },
            required = new[] { "pos_x", "pos_y", "owner_type" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null)
                return Task.FromResult(ToolResult.Error("缺少参数"));

            if (!args.Value.TryGetProperty("pos_x", out var jX) || !jX.TryGetInt32(out var posX))
                return Task.FromResult(ToolResult.Error("缺少必填参数: pos_x"));
            if (!args.Value.TryGetProperty("pos_y", out var jY) || !jY.TryGetInt32(out var posY))
                return Task.FromResult(ToolResult.Error("缺少必填参数: pos_y"));
            if (!args.Value.TryGetProperty("owner_type", out var jOt) || jOt.GetString() is not string ownerType)
                return Task.FromResult(ToolResult.Error("缺少必填参数: owner_type"));

            ownerType = ownerType.ToLowerInvariant();

            bool force = false;
            if (args.Value.TryGetProperty("force", out var jForce))
                force = jForce.ValueKind == JsonValueKind.True;

            return McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    Map map = Find.CurrentMap;
                    if (map == null)
                        return ToolResult.Error("没有当前地图。");

                    var pos = new IntVec3(posX, 0, posY);
                    var bed = pos.GetThingList(map).OfType<Building_Bed>().FirstOrDefault();
                    if (bed == null)
                        return ToolResult.Error($"({posX}, {posY}) 位置没有找到床。");

                    if (!bed.def.building.bed_humanlike)
                        return ToolResult.Error("该床不是人形床，无法设置归属类型。");

                    // 解析目标 BedOwnerType
                    BedOwnerType targetType;
                    switch (ownerType)
                    {
                        case "colonist":
                            targetType = BedOwnerType.Colonist;
                            break;
                        case "prisoner":
                            targetType = BedOwnerType.Prisoner;
                            break;
                        case "slave":
                            if (!ModsConfig.IdeologyActive)
                                return ToolResult.Error("Slave 类型需要 Ideology DLC。");
                            targetType = BedOwnerType.Slave;
                            break;
                        default:
                            return ToolResult.Error($"无效的 owner_type: {ownerType}。可选: colonist, prisoner, slave");
                    }

                    if (bed.ForOwnerType == targetType)
                        return ToolResult.Success($"该床已经是 {OwnerTypeLabel(targetType)} 类型，无需更改。");

                    var oldType = bed.ForOwnerType;

                    // 安全校验：Colonist → Prisoner/Slave 时，检查殖民者是否会无床
                    if (!force && oldType == BedOwnerType.Colonist && targetType != BedOwnerType.Colonist)
                    {
                        var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
                        if (colonists != null && colonists.Count > 0)
                        {
                            // 统计地图上所有殖民者床
                            var allColonistBeds = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                                .Where(b => b != bed && b.ForOwnerType == BedOwnerType.Colonist && b.def.building.bed_humanlike)
                                .ToList();

                            int otherColonistBeds = allColonistBeds.Count;

                            // 检查是否有殖民者被分配到此床
                            var owners = bed.OwnersForReading?.ToList() ?? new List<Pawn>();
                            var colonistOwners = owners.Where(o => o.IsColonist).ToList();

                            if (otherColonistBeds == 0)
                            {
                                if (colonistOwners.Count > 0)
                                {
                                    var names = string.Join(", ", colonistOwners.Select(o => o.Name.ToStringShort));
                                    return ToolResult.Error(
                                        $"不允许切换：这是地图上唯一的殖民者床，以下殖民者会失去床位: {names}。请先建造新的殖民者床。");
                                }
                                if (colonists.Count > 0)
                                {
                                    return ToolResult.Error(
                                        $"不允许切换：这是地图上唯一的殖民者床，{colonists.Count} 名殖民者将无床可用。请先建造新的殖民者床。");
                                }
                            }

                            // 对于每个被分配到此床的殖民者，确认有其他可用殖民者床
                            foreach (var owner in colonistOwners)
                            {
                                bool hasOtherBed = allColonistBeds.Any(b =>
                                    b != bed && (b.OwnersForReading == null || b.OwnersForReading.Count() < b.SleepingSlotsCount ||
                                                b.AnyUnownedSleepingSlot));
                                if (!hasOtherBed && otherColonistBeds == 0)
                                {
                                    return ToolResult.Error(
                                        $"不允许切换：殖民者 {owner.Name.ToStringShort} 被分配到此床，且无其他可用殖民者床。");
                                }
                            }

                            // 警告（允许但提醒）
                            if (colonistOwners.Count > 0)
                            {
                                var names = string.Join(", ", colonistOwners.Select(o => o.Name.ToStringShort));
                                var sb = new StringBuilder();
                                sb.AppendLine($"床已切换为 {OwnerTypeLabel(targetType)}。");
                                sb.AppendLine($"注意：以下殖民者失去了床的分配: {names}");
                                sb.AppendLine($"地图上仍有 {otherColonistBeds} 张殖民者床可用。");
                                return ToolResult.Success(sb.ToString().TrimEnd());
                            }
                        }
                    }

                    // 执行切换
                    bed.ForOwnerType = targetType;

                    return ToolResult.Success($"床已切换为 {OwnerTypeLabel(targetType)}。");
                }
                catch (Exception ex)
                {
                    return ToolResult.Error($"设置床类型失败: {ex.Message}");
                }
            });
        }

        public (int minX, int minZ, int maxX, int maxZ)? GetTargetRange(JsonElement? args)
        {
            if (args == null) return null;
            if (!args.Value.TryGetProperty("pos_x", out var jX) || !jX.TryGetInt32(out var px)) return null;
            if (!args.Value.TryGetProperty("pos_y", out var jY) || !jY.TryGetInt32(out var py)) return null;
            return (px, py, px, py);
        }

        private static string OwnerTypeLabel(BedOwnerType t) => t switch
        {
            BedOwnerType.Colonist => "殖民者",
            BedOwnerType.Prisoner => "俘虏",
            BedOwnerType.Slave => "奴隶",
            _ => t.ToString()
        };
    }
}
