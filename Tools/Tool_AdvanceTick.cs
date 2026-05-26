using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using RimWorld;
using RimWorldMCP.Harmony;
using Verse;

namespace RimWorldMCP.Tools
{
    public class Tool_AdvanceTick : ITool
    {
        public string Name => "advance_tick";
        public string Description => "让游戏运行指定小时数后暂停并返回游戏状态。用于观察指令执行结果，防止 LLM 过度思考。传入游戏内小时数（1 游戏小时 = 2500 tick，超快 ≈ 0.8 秒）。运行中可按空格暂停来中断。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                hours = new { type = "number", description = "要运行的游戏内小时数。1 小时 = 2500 tick，超快模式下约 0.8 秒。支持小数，如 0.5 = 半小时。推荐 0.1~0.5 小时。" }
            },
            required = new[] { "hours" }
        });

        // pending: targetTick → TCS
        private static readonly Dictionary<int, TaskCompletionSource<ToolResult>> _pending = new();
        private static readonly object _lock = new();

        /// <summary>是否有等待中的 tick advance（AutoPauseGuard 据此跳过自动暂停）</summary>
        public static bool IsActive
        {
            get { lock (_lock) return _pending.Count > 0; }
        }

        /// <summary>取消所有等待中的 advance_tick（中断按钮 / 玩家手动暂停触发）</summary>
        public static void CancelAll()
        {
            List<TaskCompletionSource<ToolResult>> cancelled;
            lock (_lock)
            {
                cancelled = new List<TaskCompletionSource<ToolResult>>(_pending.Values);
                _pending.Clear();
            }
            var tm = Find.TickManager;
            if (tm != null) tm.CurTimeSpeed = TimeSpeed.Paused;
            foreach (var tcs in cancelled)
                tcs.TrySetResult(ToolResult.Success("advance_tick 已被中断"));
        }

        /// <summary>每帧主线程调用</summary>
        public static void ProcessPending()
        {
            var tickManager = Find.TickManager;
            if (tickManager == null) return;
            int current = tickManager.TicksGame;

            List<KeyValuePair<int, TaskCompletionSource<ToolResult>>>? completed = null;
            bool playerPaused = false;

            // 高危事件 → 立即暂停，提前退出
            bool highDanger = NotificationBus.HighDangerPending;
            if (highDanger) NotificationBus.HighDangerPending = false;

            lock (_lock)
            {
                if (_pending.Count == 0) return;

                // 玩家按空格暂停 → 中断所有等待
                if (tickManager.Paused)
                {
                    playerPaused = true;
                    completed = new List<KeyValuePair<int, TaskCompletionSource<ToolResult>>>(_pending);
                    _pending.Clear();
                }
                else if (highDanger)
                {
                    completed = new List<KeyValuePair<int, TaskCompletionSource<ToolResult>>>(_pending);
                    _pending.Clear();
                }
                else
                {
                    foreach (var kv in _pending)
                    {
                        if (current >= kv.Key)
                        {
                            completed ??= new List<KeyValuePair<int, TaskCompletionSource<ToolResult>>>();
                            completed.Add(kv);
                        }
                    }
                    if (completed != null)
                    {
                        foreach (var kv in completed)
                            _pending.Remove(kv.Key);
                    }
                }
            }

            if (completed != null)
            {
                tickManager.CurTimeSpeed = TimeSpeed.Paused;

                if (playerPaused)
                {
                    foreach (var kv in completed)
                        kv.Value.TrySetResult(ToolResult.Success("advance_tick 已被中断（玩家暂停）"));
                }
                else if (highDanger)
                {
                    var dangerList = NotificationBus.Drain();
                    var sb = new StringBuilder();
                    sb.AppendLine("## 紧急事件！advance_tick 提前退出");
                    foreach (var n in dangerList)
                        sb.AppendLine($"- {n.Label}");
                    var result = ToolResult.Success(sb.ToString());
                    foreach (var kv in completed)
                        kv.Value.TrySetResult(result);
                }
                else
                {
                    var status = BuildGameStatus();
                    foreach (var kv in completed)
                        kv.Value.TrySetResult(ToolResult.Success(status));
                }
            }
        }

        private static string BuildGameStatus()
        {
            var map = Find.CurrentMap;
            var sb = new StringBuilder();
            sb.AppendLine($"## 游戏状态 (Tick {Find.TickManager?.TicksGame ?? 0})");

            if (map == null) { sb.AppendLine("当前无地图。"); return sb.ToString(); }

            var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
            sb.AppendLine($"- 殖民者: {colonists.Count} 人");

            int enemies = map.mapPawns.AllPawnsSpawned.Count(p => p.HostileTo(Faction.OfPlayer));
            if (enemies > 0) sb.AppendLine($"- 敌人: {enemies}");

            int idle = 0;
            foreach (var c in colonists)
                if (c.mindState.IsIdle) idle++;
            if (idle > 0) sb.AppendLine($"- 空闲: {idle} 人");

            return sb.ToString();
        }

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return ToolResult.Error("缺少参数");
            if (!args.Value.TryGetProperty("hours", out var jHours))
                return ToolResult.Error("缺少必填参数: hours");
            double hours = jHours.ValueKind == JsonValueKind.Number ? jHours.GetDouble() : 0;
            if (hours <= 0) return ToolResult.Error("hours 必须 > 0");
            if (hours > 24) return ToolResult.Error("hours 过大，单次最多 24 小时（1 天）");
            int ticks = (int)Math.Round(hours * 2500);

            var tcs = new TaskCompletionSource<ToolResult>();

            await McpCommandQueue.DispatchAsync<object>(() =>
            {
                var tm = Find.TickManager;
                if (tm == null)
                {
                    tcs.TrySetResult(ToolResult.Error("TickManager 不可用"));
                    return null!;
                }
                int target = tm.TicksGame + ticks;
                lock (_lock) { _pending[target] = tcs; }
                tm.CurTimeSpeed = TimeSpeed.Ultrafast;
                return null!;
            });

            return await tcs.Task;
        }
        public (int x, int y)? GetTargetPos(JsonElement? args) => null;
    }
}
