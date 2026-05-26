using System;
using System.Collections.Generic;
using System.Text.Json;

namespace RimWorldMCP
{
    public enum ChatRole { User, Assistant }
    public enum ChatState { Streaming, Done, Error }

    public enum ToolStatus { Running, Completed, Failed }
    public class ToolCallInfo
    {
        public string ItemId = "";
        public string Name = "";
        public string Title = "";
        public string Meta = "";
        public ToolStatus Status;
    }

    public class ChatEntry
    {
        public ChatRole Role;
        public string Text = "";
        public ChatState State;
        public string RunId = "";
        public string AgentId = "";
        public string AgentType = "";
        public bool IsContext; // 事件系统推送的上下文消息（空闲兜底/早报等）
        // 流式：记录上一个 delta chunk 的长度，用于 replace 场景
        public int LastChunkLen;
        // 由 UI 线程每帧写入，避免重复 Text.CalcHeight
        public float CachedHeight;
        public int CachedTextLen;
    }

    /// <summary>线程安全的聊天状态管理器，接收 Gateway 的 "chat" / "agent" 事件</summary>
    public static class ChatDisplayState
    {
        private const int MaxEntries = 100;
        private static readonly List<ChatEntry> _entries = new();
        private static readonly List<ToolCallInfo> _toolCalls = new();
        private static readonly object _lock = new();

        /// <summary>是否有活跃对话（流式输出或工具执行中），用于抑制空闲推送</summary>
        public static bool IsBusy
        {
            get
            {
                lock (_lock)
                {
                    if (_entries.Count > 0 && _entries[_entries.Count - 1].State == ChatState.Streaming)
                        return true;
                    foreach (var tc in _toolCalls)
                        if (tc.Status == ToolStatus.Running)
                            return true;
                    return false;
                }
            }
        }

        /// <summary>新消息事件（在主线程订阅）</summary>
        public static event Action? OnChanged;

        /// <summary>线程安全消息快照</summary>
        public static List<ChatEntry> Snapshot
        {
            get { lock (_lock) return new List<ChatEntry>(_entries); }
        }

        /// <summary>线程安全工具调用快照</summary>
        public static List<ToolCallInfo> ToolCallsSnapshot
        {
            get { lock (_lock) return new List<ToolCallInfo>(_toolCalls); }
        }

        /// <summary>限制条目上限，移除最旧的</summary>
        private static void TrimEntries()
        {
            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }

        /// <summary>事件系统上下文消息（空闲兜底/早报/弹框等），只显示不触发 AI</summary>
        public static void AddSystemMessage(string text)
        {
            lock (_lock)
            {
                _entries.Add(new ChatEntry
                {
                    Role = ChatRole.Assistant,
                    Text = text,
                    State = ChatState.Done,
                    IsContext = true,
                });
                TrimEntries();
            }
            OnChanged?.Invoke();
        }

        /// <summary>用户发送消息时记录（从主线程调用）</summary>
        public static void OnUserMessage(string text)
        {
            lock (_lock)
            {
                _entries.Add(new ChatEntry
                {
                    Role = ChatRole.User,
                    Text = text,
                    State = ChatState.Done
                });
                TrimEntries();
            }
            OnChanged?.Invoke();
        }

        public static void Clear()
        {
            lock (_lock)
            {
                _entries.Clear();
                _toolCalls.Clear();
            }
            OnChanged?.Invoke();
        }

        public static void ClearToolCalls()
        {
            lock (_lock) { _toolCalls.Clear(); }
            OnChanged?.Invoke();
        }

        /// <summary>将最后一个流式输出的助理条目标记为"（已中断）"，用于中断按钮</summary>
        public static void MarkLastAborted()
        {
            lock (_lock)
            {
                if (_entries.Count > 0 && _entries[_entries.Count - 1].State == ChatState.Streaming)
                {
                    _entries[_entries.Count - 1].State = ChatState.Done;
                    if (string.IsNullOrEmpty(_entries[_entries.Count - 1].Text))
                        _entries[_entries.Count - 1].Text = "（已中断）";
                }
            }
            OnChanged?.Invoke();
        }

        /// <summary>处理 Companion 广播的 SDK 消息（assistant/user 类型）</summary>
        public static void OnSdkMessage(JsonElement sdkMsg)
        {
            var message = sdkMsg.TryGetProperty("message", out var msg) ? msg : sdkMsg;
            var role = message.TryGetProperty("role", out var r) ? r.GetString() : "assistant";

            // 提取子代理信息
            var agentId = sdkMsg.TryGetProperty("agent_id", out var aid) ? aid.GetString() ?? "" : "";
            var agentType = sdkMsg.TryGetProperty("agent_type", out var at) ? at.GetString() ?? "" : "";

            if (!message.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array) return;

            foreach (var block in content.EnumerateArray())
            {
                var blockType = block.TryGetProperty("type", out var bt) ? bt.GetString() : "";
                if (blockType == "text")
                {
                    var text = block.TryGetProperty("text", out var tt) ? tt.GetString() ?? "" : "";
                    lock (_lock)
                    {
                        _entries.Add(new ChatEntry
                        {
                            Role = role == "user" ? ChatRole.User : ChatRole.Assistant,
                            Text = text,
                            State = ChatState.Done,
                            AgentId = agentId,
                            AgentType = agentType,
                        });
                        TrimEntries();
                    }
                    OnChanged?.Invoke();
                }
                else if (blockType == "tool_use")
                {
                    var name = block.TryGetProperty("name", out var tn) ? tn.GetString() ?? "" : "";
                    var itemId = block.TryGetProperty("id", out var iid) ? iid.GetString() ?? "" : "";
                    lock (_lock)
                    {
                        // 去重：同一工具 ID 不重复添加
                        bool exists = false;
                        for (int i = 0; i < _toolCalls.Count; i++)
                        {
                            if (_toolCalls[i].ItemId == itemId) { exists = true; break; }
                        }
                        if (!exists)
                        {
                            _toolCalls.Add(new ToolCallInfo
                            {
                                ItemId = itemId, Name = name, Title = name,
                                Status = ToolStatus.Running
                            });
                            while (_toolCalls.Count > 20) _toolCalls.RemoveAt(0);
                        }
                    }
                    OnChanged?.Invoke();
                }
            }
        }

    }
}
