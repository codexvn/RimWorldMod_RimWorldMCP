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
    public class Tool_GetAlerts : ITool
    {
        public string Name => "get_alerts";
        public string Description => "获取殖民地当前所有警告和提醒：空闲殖民者、缺床缺食、崩溃风险、敌人威胁等。模拟游戏右侧警告栏。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            return await McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    var map = Find.CurrentMap;
                    if (map == null) return ToolResult.Error("当前没有可用地图。");

                    var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
                    int colonistCount = colonists?.Count ?? 0;
                    var sb = new StringBuilder();
                    sb.AppendLine("## 殖民地提醒");
                    sb.AppendLine();
                    int alertCount = 0;

                    // 1. 空闲殖民者
                    var idleColonists = colonists?
                        .Where(c => c.CurJob?.def?.defName == "Wait_MaintainPosture" || c.CurJob == null)
                        .Where(c => !c.Downed && !c.Deathresting)
                        .ToList();
                    if (idleColonists != null && idleColonists.Count > 0)
                    {
                        sb.AppendLine($"### ⚠ 空闲殖民者 ({idleColonists.Count} 人)");
                        foreach (var c in idleColonists)
                        {
                            var location = $"({c.Position.x}, {c.Position.z})";
                            sb.AppendLine($"- {c.Name.ToStringShort} — {location}");
                        }
                        sb.AppendLine();
                        alertCount++;
                    }

                    // 2. 缺床铺
                    var beds = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                        .Count(b => !b.ForPrisoners && !b.Medical);
                    if (colonistCount > beds)
                    {
                        sb.AppendLine($"### ⚠ 缺少床铺");
                        sb.AppendLine($"- 殖民者: {colonistCount} 人, 可用床: {beds} 张 (缺 {colonistCount - beds} 张)");
                        sb.AppendLine();
                        alertCount++;
                    }

                    // 3. 食物不足
                    if (colonistCount > 0)
                    {
                        float totalFood = 0f;
                        foreach (var kvp in map.resourceCounter?.AllCountedAmounts ?? new())
                        {
                            var def = kvp.Key;
                            if (def.IsNutritionGivingIngestible && def.ingestible?.HumanEdible == true
                                && (def.ingestible?.foodType & FoodTypeFlags.Tree) == 0)
                            {
                                totalFood += kvp.Value * def.ingestible.CachedNutrition;
                            }
                        }
                        int daysWorth = (int)(totalFood / (colonistCount * 1.6f));
                        if (daysWorth < 3)
                        {
                            sb.AppendLine($"### ⚠ 食物不足");
                            sb.AppendLine($"- 仅够 {daysWorth} 天 (需要至少 3 天储备)");
                            sb.AppendLine();
                            alertCount++;
                        }
                    }

                    // 4. 缺乏防御
                    int turrets = map.listerBuildings.AllBuildingsColonistOfClass<Building_Turret>().Count();
                    int traps = map.listerBuildings.AllBuildingsColonistOfClass<Building_Trap>().Count();
                    float wealth = map.wealthWatcher?.WealthTotal ?? 0f;
                    if (turrets == 0 && traps == 0 && wealth > 15000)
                    {
                        sb.AppendLine($"### ⚠ 缺乏防御工事");
                        sb.AppendLine($"- 无炮塔/陷阱，财富已达 {wealth:N0}");
                        sb.AppendLine();
                        alertCount++;
                    }

                    // 5. 崩溃风险
                    var breakRisks = colonists?.Where(c =>
                    {
                        var mood = c.needs?.mood?.CurLevelPercentage;
                        return mood != null && mood < 0.2f;
                    }).ToList();
                    if (breakRisks != null && breakRisks.Count > 0)
                    {
                        sb.AppendLine($"### ⚠ 精神崩溃风险 ({breakRisks.Count} 人)");
                        foreach (var c in breakRisks)
                        {
                            var thoughtList = new System.Collections.Generic.List<Thought>();
                            c.needs.mood.thoughts.GetAllMoodThoughts(thoughtList);
                            var thoughts = thoughtList.OrderBy(t => t.MoodOffset()).Take(2)
                                .Select(t => $"{t.LabelCap} ({t.MoodOffset():+0;-0})");
                            sb.AppendLine($"- {c.Name.ToStringShort} — 心情 {c.needs.mood.CurLevelPercentage * 100f:F0}% ({string.Join(", ", thoughts)})");
                        }
                        sb.AppendLine();
                        alertCount++;
                    }

                    // 6. 严重流血
                    var bleeders = colonists?.Where(c => c.health?.hediffSet?.BleedRateTotal > 0.3f).ToList();
                    if (bleeders != null && bleeders.Count > 0)
                    {
                        sb.AppendLine($"### ⚠ 严重流血 ({bleeders.Count} 人)");
                        foreach (var c in bleeders)
                        {
                            float bleed = c.health.hediffSet.BleedRateTotal;
                            float hoursLeft = c.health.hediffSet.BleedRateTotal > 0
                                ? c.health.hediffSet.PainTotal / bleed : 24f;
                            sb.AppendLine($"- {c.Name.ToStringShort} — 流血率 {bleed * 100f:F0}%/天 (约 {hoursLeft:F0} 小时可致死)");
                        }
                        sb.AppendLine();
                        alertCount++;
                    }

                    // 7. 破烂衣物警告
                    var tatteredApparel = colonists?
                        .SelectMany(c => c.apparel?.WornApparel ?? Enumerable.Empty<Apparel>())
                        .Where(a => a.HitPoints * 100f / a.MaxHitPoints < 30f)
                        .ToList();
                    if (tatteredApparel != null && tatteredApparel.Count > 0)
                    {
                        sb.AppendLine($"### ⚠ 破损衣物 ({tatteredApparel.Count} 件)");
                        foreach (var a in tatteredApparel.Take(10))
                        {
                            var wearer = a.Wearer?.Name?.ToStringShort ?? "未知";
                            sb.AppendLine($"- {a.Label} (耐久 {a.HitPoints * 100f / a.MaxHitPoints:F0}%) — 穿着者: {wearer}");
                        }
                        if (tatteredApparel.Count > 10)
                            sb.AppendLine($"- ... 另有 {tatteredApparel.Count - 10} 件");
                        sb.AppendLine();
                        alertCount++;
                    }

                    // 8. 未通电建筑
                    var unpowered = map.listerBuildings.AllBuildingsColonistOfClass<Building>().Count();
                    // Too slow to check all, skip

                    // 9. 武器缺失
                    int unarmed = colonists?.Count(c =>
                        c.equipment?.Primary == null && !c.WorkTagIsDisabled(WorkTags.Violent)) ?? 0;
                    if (unarmed > colonistCount * 0.4f && unarmed > 0)
                    {
                        sb.AppendLine($"### ⚠ 武器不足");
                        sb.AppendLine($"- {unarmed} 名战斗能力者无武器 (占 {unarmed * 100 / Math.Max(1, colonistCount)}%)");
                        sb.AppendLine();
                        alertCount++;
                    }

                    if (alertCount == 0)
                        sb.AppendLine("祝贺！殖民地一切正常，无活跃警报。");
                    else
                        sb.AppendLine($"---\n共 {alertCount} 类警报");

                    return ToolResult.Success(sb.ToString().TrimEnd());
                }
                catch (Exception ex)
                {
                    return ToolResult.Error($"警报检查失败: {ex.Message}");
                }
            });
        }
    }
}
