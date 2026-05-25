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

    /// <summary>消息队列 — 同类覆盖 + 等 agent stream 完成才发下一条</summary>
    public static class GatewayMessageQueue
    {
        private static readonly Dictionary<MessageCategory, PendingMessage> _pending = new();
        private static bool _sending;
        private static MessageCategory _currentSendingCategory;
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

        /// <summary>每帧调用</summary>
        public static void Tick()
        {
            if (!GatewayClient.IsConnected)
            {
                _sending = false;
                return;
            }

            if (!GatewayClient.IsReady) return;

            // 正在发送中——取消当前 agent 等待，让通知立即抢占
            if (_sending || GatewayClient.IsSendingMessage)
            {
                if (_pending.Count > 0)
                    GatewayClient.CancelCurrentSend();
                return;
            }

            if (_pending.Count == 0) return;

            // 取最高优先级发送（SendMessage 内部统一 abort）
            var bestMsg = _pending.Values.OrderByDescending(m => (int)m.Category).First();
            _pending.Remove(bestMsg.Category);
            _ = DoSend(bestMsg.Category, bestMsg.Text);
        }

        public static void SendNow(MessageCategory category, string text)
        {
            if (!GatewayClient.IsReady) return;

            if (!_sending)
            {
                _ = DoSend(category, text);
                return;
            }

            // 正在发送中——取消当前 agent 等待，让通知立即抢占
            GatewayClient.CancelCurrentSend();
            _pending[category] = new PendingMessage { Category = category, Text = text };
        }

        public static void MarkDailySent(int day) => _lastDailyDaySent = day;
        public static bool WasDailySentToday(int day) => _lastDailyDaySent == day;
        public static void MarkSessionPromptSent() => _sessionPromptSent = true;
        public static bool WasSessionPromptSent => _sessionPromptSent;

        public static void Reset()
        {
            _pending.Clear();
            _sending = false;
            _lastSendRealMs = 0;
            _lastDailyDaySent = -1;
            _sessionPromptSent = false;
        }

        private static async System.Threading.Tasks.Task DoSend(MessageCategory category, string text)
        {
            if (!GatewayClient.IsReady) return;
            _sending = true;
            _currentSendingCategory = category;
            try
            {
                await GatewayClient.SendMessage(text);
                _lastSendRealMs = Environment.TickCount;
            }
            catch (OperationCanceledException)
            {
                // 被更高优先级通知抢占，不记日志
            }
            catch (Exception ex)
            {
                McpLog.Warn($"[queue] 发送失败 ({category}): {ex.Message}");
            }
            finally
            {
                _sending = false;
            }
        }
    }
}
