using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;
using RimWorld;

namespace RimWorldMCP
{
    public static class GatewayEventMonitor
    {
        private static int _nextCheckTick;
        private const int CheckIntervalTicks = 120;
        private static int _lastColonistCount = -1;
        private static int _lastIdleCount = -1;
        private static HashSet<int> _seenLetterIds = new();
        private static HashSet<string> _seenMessageIds = new();
        private static FieldInfo? _msgStartingTimeField;
        public static readonly ConcurrentQueue<string> RecentMessages = new();

        public static void Reset()
        {
            _seenLetterIds.Clear();
            _seenMessageIds.Clear();
        }

        public static void Tick()
        {
            if (!GatewayClient.IsConnected) return;
            var tick = Find.TickManager?.TicksGame ?? 0;
            if (tick < _nextCheckTick) return;
            _nextCheckTick = tick + CheckIntervalTicks;

            var map = Find.CurrentMap;
            if (map == null) return;

            var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
            int colonistCount = colonists.Count;

            // === 1. Letter 通知监控 ===
            CheckNewLetters(map, colonists, colonistCount);

            // === 2. 右侧消息监控 ===
            CheckNewMessages();

            // === 3. 空闲殖民者检测 ===
            int idleCount = colonists.Count(c =>
                (c.CurJob?.def?.defName == "Wait_MaintainPosture" || c.CurJob == null)
                && !c.Downed && !c.Deathresting);
            bool hasNewIdle = idleCount > _lastIdleCount && idleCount > 0;
            _lastIdleCount = idleCount;

            // === 3. 殖民者数量变化 ===
            bool countChanged = colonistCount != _lastColonistCount && _lastColonistCount >= 0;
            _lastColonistCount = colonistCount;

            // === 4. 综合警报 ===
            var alerts = BuildAlertLines(map, colonists, colonistCount);
            if (hasNewIdle)
            {
                var names = colonists
                    .Where(c => (c.CurJob?.def?.defName == "Wait_MaintainPosture" || c.CurJob == null)
                        && !c.Downed && !c.Deathresting)
                    .Take(5).Select(c => c.Name.ToStringShort);
                alerts.Add($"{(idleCount > 1 ? $"{idleCount} 名" : "")}殖民者空闲: {string.Join(", ", names)}");
            }
            if (countChanged)
            {
                int diff = colonistCount - _lastColonistCount;
                alerts.Add($"殖民者数量: {_lastColonistCount} → {colonistCount} ({(diff > 0 ? "+" : "")}{diff})");
            }

            if (alerts.Count > 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine("## ⚠ 殖民地警报");
                foreach (var a in alerts)
                    sb.AppendLine($"- {a}");
                sb.Append(BuildColonySummary(map, colonists, colonistCount));
                GatewayMessageQueue.Enqueue(MessageCategory.Alert, sb.ToString().TrimEnd());
            }

            // === 5. 早报（游戏时间每天早上 6 点） ===
            int hour = GenLocalDate.HourOfDay(map);
            int day = tick / 60000;
            if (hour == 6 && !GatewayMessageQueue.WasDailySentToday(day))
            {
                GatewayMessageQueue.MarkDailySent(day);
                var msg = BuildDailyOverview(map, colonists, colonistCount, tick);
                GatewayMessageQueue.Enqueue(MessageCategory.DailyMorning, msg);
            }
        }

        // ========== Letter 通知监控 ==========

        private static void CheckNewLetters(Map map, List<Pawn> colonists, int colonistCount)
        {
            var letters = Find.LetterStack.LettersListForReading;
            var currentIds = new HashSet<int>(letters.Select(l => l.ID));

            // 首轮初始化：标记已有 Letter 为已见，不触发通知
            if (_seenLetterIds.Count == 0)
            {
                _seenLetterIds = currentIds;
                return;
            }

            // 检测新 Letter
            foreach (var letter in letters)
            {
                if (!_seenLetterIds.Contains(letter.ID))
                {
                    OnNewLetter(letter, map, colonists, colonistCount);
                }
            }

            // 清理已关闭的 Letter ID
            _seenLetterIds.IntersectWith(currentIds);
        }

        private static void OnNewLetter(Letter letter, Map map, List<Pawn> colonists, int colonistCount)
        {
            var sb = new StringBuilder();
            string dangerLabel = GetDangerLabel(letter.def);
            sb.AppendLine($"## [{dangerLabel}] {letter.Label.Resolve()}");

            // 提取正文（ChoiceLetter 才有 Text）
            if (letter is ChoiceLetter choiceLetter)
            {
                string text = choiceLetter.Text.Resolve();
                if (!string.IsNullOrEmpty(text))
                {
                    // 只取前 500 字符，太长截断
                    if (text.Length > 500)
                        text = text.Substring(0, 497) + "...";
                    sb.AppendLine(text);
                }
            }

            sb.Append(BuildColonySummary(map, colonists, colonistCount));

            // 大威胁立即发送，其余排队
            bool isBigThreat = letter.def == LetterDefOf.ThreatBig;
            var category = isBigThreat ? MessageCategory.RaidStart : MessageCategory.Alert;
            string msg = sb.ToString().TrimEnd();

            if (isBigThreat)
                GatewayMessageQueue.SendNow(category, msg);
            else
                GatewayMessageQueue.Enqueue(category, msg);
        }

        private static string GetDangerLabel(LetterDef def)
        {
            if (def == LetterDefOf.ThreatBig) return "大威胁";
            if (def == LetterDefOf.ThreatSmall) return "小威胁";
            if (def == LetterDefOf.NegativeEvent) return "负面";
            if (def == LetterDefOf.PositiveEvent) return "正面";
            if (def == LetterDefOf.Death) return "死亡";
            if (def == LetterDefOf.NeutralEvent) return "事件";
            return "通知";
        }

        // ========== 右侧消息监控 ==========

        private static void CheckNewMessages()
        {
            var archivables = Find.Archive?.ArchivablesListForReading;
            if (archivables == null) return;

            var currentIds = new HashSet<string>();
            var newMessages = new List<Verse.Message>();

            foreach (var a in archivables)
            {
                if (a is not Verse.Message msg) continue;
                string id = ((ILoadReferenceable)msg).GetUniqueLoadID();
                currentIds.Add(id);
                if (!_seenMessageIds.Contains(id))
                    newMessages.Add(msg);
            }

            // 首轮初始化：标记所有已有消息为已见
            if (_seenMessageIds.Count == 0)
            {
                _seenMessageIds = currentIds;
                return;
            }

            if (newMessages.Count > 0)
            {
                foreach (var msg in newMessages)
                {
                    string id = ((ILoadReferenceable)msg).GetUniqueLoadID();
                    _seenMessageIds.Add(id);
                    if (string.IsNullOrEmpty(msg.text)) continue;

                    string label = GetMessageTypeLabel(msg.def);
                    string text = msg.text.Length > 300
                        ? msg.text.Substring(0, 297) + "..."
                        : msg.text;
                    GatewayMessageQueue.Enqueue(MessageCategory.Alert, $"[{label}] {text}");
                    RecentMessages.Enqueue($"[{label}] {text}");
                    Find.Archive!.Remove(msg);
                    ExpireLiveMessage(msg);
                }
            }

            // 清理已归档的消息 ID
            _seenMessageIds.IntersectWith(currentIds);
        }

        private static string GetMessageTypeLabel(MessageTypeDef def)
        {
            if (def == MessageTypeDefOf.ThreatBig) return "大威胁";
            if (def == MessageTypeDefOf.ThreatSmall) return "小威胁";
            if (def == MessageTypeDefOf.PawnDeath) return "角色死亡";
            if (def == MessageTypeDefOf.NegativeHealthEvent) return "健康事件";
            if (def == MessageTypeDefOf.NegativeEvent) return "负面";
            if (def == MessageTypeDefOf.NeutralEvent) return "事件";
            if (def == MessageTypeDefOf.PositiveEvent) return "正面";
            if (def == MessageTypeDefOf.TaskCompletion) return "完成";
            if (def == MessageTypeDefOf.SituationResolved) return "状态解除";
            if (def == MessageTypeDefOf.RejectInput) return "拒绝";
            if (def == MessageTypeDefOf.CautionInput) return "警告";
            return def?.defName ?? "消息";
        }

        private static void ExpireLiveMessage(Verse.Message msg)
        {
            _msgStartingTimeField ??= typeof(Verse.Message).GetField("startingTime",
                BindingFlags.Instance | BindingFlags.NonPublic);
            _msgStartingTimeField?.SetValue(msg, -99999f);
        }

        /// <summary>立即读取 Find.Archive 中尚未处理的 Message，捕获 CheckNewMessages 周期间的消息</summary>
        public static string DrainUnprocessedMessages()
        {
            var archivables = Find.Archive?.ArchivablesListForReading;
            if (archivables == null || _seenMessageIds.Count == 0) return "";

            var sb = new StringBuilder();
            foreach (var a in archivables)
            {
                if (a is not Verse.Message msg) continue;
                string id = ((ILoadReferenceable)msg).GetUniqueLoadID();
                if (_seenMessageIds.Contains(id)) continue;
                if (string.IsNullOrEmpty(msg.text)) continue;

                _seenMessageIds.Add(id);
                string label = GetMessageTypeLabel(msg.def);
                string text = msg.text.Length > 300
                    ? msg.text.Substring(0, 297) + "..."
                    : msg.text;
                string formatted = $"[{label}] {text}";
                RecentMessages.Enqueue(formatted);
                sb.AppendLine($"- {formatted}");
                ExpireLiveMessage(msg);
            }
            return sb.ToString().TrimEnd();
        }

        // ========== 消息构建 ==========

        private static string BuildDailyOverview(Map map, List<Pawn> colonists, int colonistCount, int ticksGame)
        {
            var sb = new StringBuilder();
            int day = ticksGame / 60000;
            int year = day / 15 + 1;
            int dayOfYear = day % 15 + 1;

            var season = GenLocalDate.Season(map);
            string seasonStr = season switch
            {
                Season.Spring => "春", Season.Summer => "夏",
                Season.Fall => "秋", Season.Winter => "冬", _ => "?"
            };
            sb.AppendLine($"## 每早汇报 第{year}年 {seasonStr}季 第{dayOfYear}天");

            // 天气
            var weather = map.weatherManager?.curWeather;
            float temp = map.mapTemperature?.OutdoorTemp ?? 0f;
            sb.AppendLine($"天气: {weather?.label ?? "?"}, 室外 {temp:F0}°C");

            // 殖民者
            float avgMood = colonists.Count > 0
                ? colonists.Average(c => c.needs?.mood?.CurLevelPercentage ?? 0.5f) * 100f
                : 0f;
            sb.AppendLine($"殖民者: {colonistCount} 人 | 平均心情 {avgMood:F0}%");

            // 资源
            int steel = GetCountByDefName(map, "Steel");
            int wood = GetCountByDefName(map, "WoodLog");
            int components = GetCountByDefName(map, "ComponentIndustrial");
            int silver = GetCountByDefName(map, "Silver");
            int foodDays = CalcFoodDays(map, colonistCount);
            sb.AppendLine($"资源: 钢{steel} 木{wood} 零件{components} 银{silver} | 食物约{foodDays}天");

            // 电力
            float generated = 0, used = 0, stored = 0;
            foreach (var net in map.powerNetManager?.AllNetsListForReading ?? new List<PowerNet>())
            {
                foreach (var comp in net.powerComps)
                {
                    if (!comp.PowerOn) continue;
                    float rate = comp.EnergyOutputPerTick;
                    if (rate > 0) generated += rate; else used += -rate;
                }
                stored += net.CurrentStoredEnergy();
            }
            string powerLabel = generated >= used ? "盈余" : "赤字";
            sb.AppendLine($"电力: 发{generated / 1000f:F0}kW 用{used / 1000f:F0}kW 储{stored / 1000f:F0}kWd ({powerLabel})");

            // 研究
            var rm = Find.ResearchManager;
            var curProj = rm?.GetProject();
            if (curProj != null)
                sb.AppendLine($"研究: {curProj.label} ({rm!.GetProgress(curProj) * 100f:F0}%)");
            else
                sb.AppendLine("研究: 无");

            // 财富
            float wealth = map.wealthWatcher?.WealthTotal ?? 0f;
            sb.AppendLine($"财富: {wealth:N0}");

            // 警报
            var alerts = BuildAlertLines(map, colonists, colonistCount);
            if (alerts.Count > 0)
            {
                sb.AppendLine("警报:");
                foreach (var a in alerts)
                    sb.AppendLine($"  - {a}");
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>殖民地概要（附加在消息末尾）</summary>
        private static string BuildColonySummary(Map map, List<Pawn> colonists, int colonistCount)
        {
            var sb = new StringBuilder();
            int foodDays = CalcFoodDays(map, colonistCount);
            float avgMood = colonists.Count > 0
                ? colonists.Average(c => c.needs?.mood?.CurLevelPercentage ?? 0.5f) * 100f
                : 0f;

            sb.AppendLine($"---");
            sb.AppendLine($"殖民者: {colonistCount}人 | 心情: {avgMood:F0}% | 食物: {foodDays}天");

            int steel = GetCountByDefName(map, "Steel");
            int components = GetCountByDefName(map, "ComponentIndustrial");
            sb.AppendLine($"钢{steel} | 零件{components}");

            return sb.ToString();
        }

        // ========== 警报提取（check_colony 同款逻辑） ==========

        private static List<string> BuildAlertLines(Map map, List<Pawn> colonists, int colonistCount)
        {
            var alerts = new List<string>();

            // 崩溃风险
            var breakRisks = colonists
                .Where(c => (c.needs?.mood?.CurLevelPercentage ?? 1f) < 0.2f)
                .Select(c => $"崩溃风险: {c.Name.ToStringShort} 心情{(c.needs!.mood!.CurLevelPercentage * 100f):F0}%")
                .ToList();
            alerts.AddRange(breakRisks);

            // 严重流血
            var bleeders = colonists
                .Where(c => (c.health?.hediffSet?.BleedRateTotal ?? 0f) > 0.3f)
                .Select(c => $"严重流血: {c.Name.ToStringShort} 失血率{(c.health!.hediffSet.BleedRateTotal * 100f):F0}%/天")
                .ToList();
            alerts.AddRange(bleeders);

            // 逃跑中
            var fleeing = colonists
                .Where(c => c.MentalState?.def == MentalStateDefOf.PanicFlee)
                .Select(c => $"逃跑中: {c.Name.ToStringShort}")
                .ToList();
            alerts.AddRange(fleeing);

            // 食物不足
            int foodDays = CalcFoodDays(map, colonistCount);
            if (foodDays < 3 && colonistCount > 0)
                alerts.Add($"食物不足: 仅够 {foodDays} 天");

            // 无防御
            int turrets = map.listerBuildings.AllBuildingsColonistOfClass<Building_Turret>().Count();
            int traps = map.listerBuildings.AllBuildingsColonistOfClass<Building_Trap>().Count();
            float wealth = map.wealthWatcher?.WealthTotal ?? 0f;
            if (turrets == 0 && traps == 0 && wealth > 15000)
                alerts.Add($"无防御工事 (财富{wealth:N0})");

            // 缺床
            int beds = map.listerBuildings.AllBuildingsColonistOfClass<Building_Bed>()
                .Count(b => !b.ForPrisoners && !b.Medical);
            if (colonistCount > beds)
                alerts.Add($"缺床: {colonistCount}人仅{beds}张");

            return alerts;
        }

        // ========== 工具方法 ==========

        private static int GetCountByDefName(Map map, string defName)
        {
            var resources = map.resourceCounter?.AllCountedAmounts;
            if (resources == null) return 0;
            foreach (var kv in resources)
                if (kv.Key.defName == defName)
                    return kv.Value;
            return 0;
        }

        private static int CalcFoodDays(Map map, int colonistCount)
        {
            if (colonistCount <= 0) return 999;
            float total = 0f;
            foreach (var kv in map.resourceCounter?.AllCountedAmounts ?? new Dictionary<ThingDef, int>())
            {
                var def = kv.Key;
                if (def.IsNutritionGivingIngestible && def.ingestible?.HumanEdible == true
                    && (def.ingestible?.foodType & FoodTypeFlags.Tree) == 0)
                    total += kv.Value * (def.ingestible?.CachedNutrition ?? 0f);
            }
            return (int)(total / (colonistCount * 1.6f));
        }
    }
}
