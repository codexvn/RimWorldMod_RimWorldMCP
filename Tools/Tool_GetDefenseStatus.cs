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
    public class Tool_GetDefenseStatus : ITool
    {
        public string Name => "get_defense_status";
        public string Description => "获取殖民地防御状态报告：所有殖民者的武器装备、护甲覆盖、征召状态和战斗力评估。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            return await McpCommandQueue.DispatchAsync(() =>
            {
                var map = Find.CurrentMap;
                if (map == null)
                    return ToolResult.Error("当前没有可用地图。");

                var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
                var sb = new StringBuilder();
                sb.AppendLine("## 防御状态报告");
                sb.AppendLine();

                // === 武器装备 ===
                sb.AppendLine("### 武器装备");
                sb.AppendLine();
                sb.AppendLine("| 殖民者 | 主武器 | 护甲 | 征召 |");
                sb.AppendLine("|--------|--------|------|------|");

                int rangedCount = 0;
                int meleeCount = 0;
                int unarmedCount = 0;
                int armoredCount = 0;

                foreach (var pawn in colonists)
                {
                    var name = pawn.Name?.ToStringShort ?? pawn.LabelShortCap;
                    var drafted = pawn.Drafted;
                    var draftedText = drafted ? "已征召" : "未征召";

                    // 主武器
                    var weapon = "-";
                    var equipment = pawn.equipment;
                    if (equipment != null && equipment.Primary != null)
                    {
                        var primary = equipment.Primary;
                        weapon = primary.def?.label ?? primary.def?.defName ?? "???";
                        var quality = primary.TryGetComp<CompQuality>();
                        if (quality != null)
                            weapon += $" ({quality.Quality.GetLabel()})";

                        // 判断远程还是近战
                        if (primary.def?.IsRangedWeapon == true)
                            rangedCount++;
                        else if (primary.def?.IsMeleeWeapon == true)
                            meleeCount++;
                    }
                    else
                    {
                        unarmedCount++;
                    }

                    // 护甲
                    var armorParts = new List<string>();
                    var apparel = pawn.apparel;
                    if (apparel != null)
                    {
                        var wornApparel = apparel.WornApparel;
                        if (wornApparel != null)
                        {
                            foreach (var ap in wornApparel)
                            {
                                var apLabel = ap.def?.label ?? ap.def?.defName ?? "???";
                                var apQuality = ap.TryGetComp<CompQuality>();
                                var qualitySuffix = apQuality != null ? $" ({apQuality.Quality.GetLabel()})" : "";
                                armorParts.Add($"{apLabel}{qualitySuffix}");
                            }
                        }
                    }

                    if (armorParts.Count > 0)
                        armoredCount++;

                    var armorText = armorParts.Count > 0 ? string.Join(", ", armorParts) : "无";

                    sb.AppendLine($"| {name} | {weapon} | {armorText} | {draftedText} |");
                }

                // === 战斗力评估 ===
                sb.AppendLine();
                sb.AppendLine("### 战斗力评估");
                sb.AppendLine();
                sb.AppendLine($"- 远程火力: {rangedCount} 人");
                sb.AppendLine($"- 近战单位: {meleeCount} 人");
                sb.AppendLine($"- 无武器: {unarmedCount} 人");
                sb.AppendLine($"- 有护甲: {armoredCount} 人");

                // === 阵地设施 ===
                sb.AppendLine();
                sb.AppendLine("### 阵地设施");
                sb.AppendLine();

                // 统计炮塔
                var turrets = map.listerBuildings.AllBuildingsColonistOfClass<Building_Turret>()
                    .Where(t => t.GetComp<CompPowerTrader>() != null)
                    .ToList();

                var poweredTurrets = turrets.Count(t =>
                {
                    var comp = t.GetComp<CompPowerTrader>();
                    return comp != null && comp.PowerOn;
                });
                var totalTurrets = turrets.Count;

                // 统计陷阱
                var traps = map.listerBuildings.AllBuildingsColonistOfClass<Building_Trap>();
                var totalTraps = traps?.Count() ?? 0;

                // 统计沙袋
                var sandbags = map.listerBuildings.AllBuildingsColonistOfDef(ThingDefOf.Sandbags);
                var totalSandbags = sandbags?.Count ?? 0;

                sb.AppendLine($"- 炮塔: {totalTurrets} 座 (已供电: {poweredTurrets})");
                sb.AppendLine($"- 陷阱: {totalTraps} 个");
                sb.AppendLine($"- 沙袋: {totalSandbags} 格");

                // === 防御建议 ===
                sb.AppendLine();
                sb.AppendLine("### 防御建议");
                sb.AppendLine();

                var recommendations = new List<string>();

                if (unarmedCount > 0)
                    recommendations.Add($"有 {unarmedCount} 名殖民者未装备武器，建议配备基础远程武器。");

                if (rangedCount < colonists.Count * 0.6f)
                    recommendations.Add("远程火力覆盖不足，建议提升远程战斗人员比例。");

                if (armoredCount < colonists.Count * 0.5f)
                    recommendations.Add("多数殖民者缺少护甲防护，建议制作简易头盔和防弹背心。");

                if (totalTurrets == 0)
                    recommendations.Add("当前无炮塔防御，建议在阵地部署至少2-3座炮塔。");

                if (totalTraps == 0)
                    recommendations.Add("当前无陷阱，建议在关键通道布设陷阱减缓敌人推进。");

                if (recommendations.Count == 0)
                    sb.AppendLine("防御状态良好，暂无紧急建议。");
                else
                {
                    for (int i = 0; i < recommendations.Count; i++)
                        sb.AppendLine($"{i + 1}. {recommendations[i]}");
                }

                return ToolResult.Success(sb.ToString());
            });
        }
    }
}
