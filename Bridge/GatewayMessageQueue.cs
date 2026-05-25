using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimWorldMCP
{
    public enum MessageCategory
    {
        DailyMorning = 0,
        Alert = 10,
        DialogPrompt = 25,
        RaidEnd = 20,
        RaidStart = 30,
        SessionInit = 40,
    }

    internal struct PendingMessage
    {
        public MessageCategory Category;
        public string Text;
    }

    /// <summary>消息队列 — 同类覆盖 + SendMessage 统一并发控制</summary>
    public static class GatewayMessageQueue
    {
        private static readonly Dictionary<MessageCategory, PendingMessage> _pending = new();
        private static int _lastSendRealMs;
        private static int _lastDailyDaySent = -1;
        private static bool _sessionPromptSent;

        /// <summary>最后一次成功发送 agent 消息时的真实时间（ms）</summary>
        public static int LastSendRealMs => _lastSendRealMs;

        public static void Enqueue(MessageCategory category, string text)
        {
            if (!GatewayClient.IsConnected) return;
            _pending[category] = new PendingMessage { Category = category, Text = text };
        }

        /// <summary>每帧调用 — 有通知且正在发送时打断，空闲时发送下一条</summary>
        public static void Tick()
        {
            if (!GatewayClient.IsReady) return;

            if (GatewayClient.IsSendingMessage)
            {
                // 正在发送中——有通知即打断当前 agent
                if (_pending.Count > 0)
                    GatewayClient.AbortAgent();
                return;
            }

            if (_pending.Count == 0) return;

            // 取最高优先级，SendMessage 内部 _messageLock + sessions.steer 保证原子性
            var bestMsg = _pending.Values.OrderByDescending(m => (int)m.Category).First();
            _pending.Remove(bestMsg.Category);
            SendPending(bestMsg.Category, bestMsg.Text);
        }

        public static void SendNow(MessageCategory category, string text)
        {
            if (!GatewayClient.IsReady) return;

            if (GatewayClient.IsSendingMessage)
                GatewayClient.AbortAgent();

            _pending[category] = new PendingMessage { Category = category, Text = text };

            // 如果当前没有发送中，立即发
            if (!GatewayClient.IsSendingMessage)
            {
                if (_pending.TryGetValue(category, out var pm) && pm.Text == text)
                {
                    _pending.Remove(category);
                    SendPending(pm.Category, pm.Text);
                }
            }
        }

        public static void MarkDailySent(int day) => _lastDailyDaySent = day;
        public static bool WasDailySentToday(int day) => _lastDailyDaySent == day;
        public static void MarkSessionPromptSent() => _sessionPromptSent = true;
        public static bool WasSessionPromptSent => _sessionPromptSent;

        public static void Reset()
        {
            _pending.Clear();
            _lastSendRealMs = 0;
            _lastDailyDaySent = -1;
            _sessionPromptSent = false;
        }

        private static async void SendPending(MessageCategory category, string text)
        {
            try
            {
                await GatewayClient.SendMessage(text);
                _lastSendRealMs = Environment.TickCount;
            }
            catch (Exception ex)
            {
                McpLog.Warn($"[queue] 发送失败 ({category}): {ex.Message}");
            }
        }
    }
}
