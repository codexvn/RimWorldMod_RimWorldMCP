using System;
using System.Collections.Generic;
using RimWorld;
using RimWorldMCP.Helpers;
using UnityEngine;
using Verse;

namespace RimWorldMCP
{
    /// <summary>
    /// AI 对话窗口 — 双栏布局：左栏对话流，右栏工具调用记录
    /// </summary>
    public class Dialog_AiChat : Window
    {
        private Vector2 _chatScrollPos;
        private Vector2 _toolScrollPos;
        private bool _scrollToBottom;
        private string _inputText = "";
        private static float _alpha = 0.85f;

        private static readonly Color UserBgColor = new Color(0.12f, 0.18f, 0.30f, 1f);
        private static readonly Color AiBgColor = new Color(0.08f, 0.22f, 0.10f, 1f);
        private static readonly Color SubagentBgColor = new Color(0.15f, 0.08f, 0.22f, 1f);
        private static readonly Color ErrorBgColor = new Color(0.30f, 0.08f, 0.08f, 1f);
        private static Color ToolCardBg => new Color(0.08f, 0.10f, 0.18f, _alpha);
        private static Color ToolCardHeaderBg => new Color(0.10f, 0.13f, 0.22f, _alpha);

        protected override float Margin => 6f;

        public Dialog_AiChat()
        {
            optionalTitle = "RimWorld AI Commander";
            doCloseX = true;
            closeOnCancel = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            draggable = true;
            resizeable = true;
            forcePause = false;
            layer = WindowLayer.Dialog;
            preventCameraMotion = false;
            doWindowBackground = true;
            drawShadow = true;
        }

        public override Vector2 InitialSize =>
            new Vector2(UI.screenWidth / 3f + 160f, UI.screenHeight / 3f + 80f);

        protected override void SetInitialSizeAndPosition()
        {
            windowRect = new Rect(UI.screenWidth - InitialSize.x - 10f, 10f,
                InitialSize.x, InitialSize.y);
            windowRect = windowRect.Rounded();
        }

        public override void PreOpen()
        {
            base.PreOpen();
            ChatDisplayState.OnChanged += OnChatChanged;
        }

        public override void PostClose()
        {
            ChatDisplayState.OnChanged -= OnChatChanged;
            base.PostClose();
        }

        private bool _toolScrollToBottom;
        private void OnChatChanged() { _scrollToBottom = true; _toolScrollToBottom = true; }

        private void TrySendInput()
        {
            var text = _inputText.Trim();
            if (string.IsNullOrEmpty(text)) return;

            if (!CCClient.IsReady)
            {
                Messages.Message("Claude Code 未连接", MessageTypeDefOf.RejectInput, false);
                return;
            }

            _inputText = "";
            _ = CCClient.SendAbort();
            ChatDisplayState.OnUserMessage(text);
            _ = CCClient.SendEventText("rimworld.chat", "UserMessage", text);
        }

        // ========== 主布局 ==========

        public override void DoWindowContents(Rect inRect)
        {
            if (Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                TrySendInput();
                Event.current.Use();
            }

            var entries = ChatDisplayState.Snapshot;
            var toolCalls = ChatDisplayState.ToolCallsSnapshot;

            float headerH = 22f;
            float inputH = 28f;
            float footerH = 22f;
            float gap = 4f;
            float panelGap = 6f;
            float leftRatio = 0.60f;

            // Header
            DrawHeader(new Rect(inRect.x, inRect.y, inRect.width, headerH));

            // Panels
            float panelsY = inRect.y + headerH + gap;
            float panelsH = inRect.height - headerH - inputH - footerH - gap * 3;
            float leftW = (inRect.width - panelGap) * leftRatio;
            float rightW = inRect.width - leftW - panelGap;

            // 分隔线（细条）
            float dividerX = inRect.x + leftW + panelGap / 2f;
            Widgets.DrawBoxSolid(new Rect(dividerX, panelsY, 1f, panelsH),
                new Color(0.3f, 0.3f, 0.3f, _alpha));

            DrawConversationPanel(
                new Rect(inRect.x, panelsY, leftW, panelsH), entries);
            DrawToolPanel(
                new Rect(dividerX + panelGap / 2f + 1f, panelsY, inRect.width - leftW - panelGap - 2f, panelsH),
                toolCalls);

            // Input
            float inputY = panelsY + panelsH + gap;
            DrawInputRow(new Rect(inRect.x, inputY, inRect.width, inputH));

            // Footer
            float footerY = inputY + inputH + gap;
            DrawFooter(new Rect(inRect.x, footerY, inRect.width, footerH));
        }

        // ========== 顶栏 ==========

        private static void DrawHeader(Rect rect)
        {
            string colony = "未知殖民地";
            try
            {
                var map = Find.CurrentMap;
                if (map != null)
                {
                    var parent = map.Parent;
                    if (parent != null && !parent.Label.NullOrEmpty())
                        colony = parent.Label;
                    else if (Find.World?.info?.name != null)
                        colony = Find.World.info.name;
                }
            }
            catch { }

            string dayInfo = "";
            try
            {
                var map = Find.CurrentMap;
                if (map != null)
                {
                    var season = GenLocalDate.Season(map);
                    int dayOfQ = GenLocalDate.DayOfQuadrum(map);
                    int year = GenLocalDate.Year(map);
                    dayInfo = $" | {season} {dayOfQ}, {year}";
                }
            }
            catch { }

            string agent = RimWorldMCPMod.Instance?.Settings?.CCModelName ?? "Claude";
            int shortIdx = agent.LastIndexOf('/');
            if (shortIdx >= 0) agent = agent.Substring(shortIdx + 1);
            if (agent.Length > 28) agent = agent.Substring(0, 28);

            string header = $"{colony}{dayInfo} | Agent: {agent}";
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.8f, 0.5f, _alpha);
            Widgets.Label(new Rect(rect.x, rect.y + 2f, rect.width, rect.height - 2f), header);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // 底部分隔线
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax, rect.width, 1f),
                new Color(0.25f, 0.25f, 0.25f, _alpha));
        }

        // ========== 左栏：对话流 ==========

        private void DrawConversationPanel(Rect panelRect, List<ChatEntry> entries)
        {
            // 标题
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, _alpha);
            Widgets.Label(new Rect(panelRect.x, panelRect.y, 100f, 16f), "对话");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect scrollRect = new Rect(panelRect.x, panelRect.y + 14f,
                panelRect.width, panelRect.height - 14f);

            if (entries.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.4f, 0.4f, 0.4f, _alpha);
                Widgets.Label(new Rect(scrollRect.x, scrollRect.y + 4f,
                    scrollRect.width, 16f), "等待 AI 回应...");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            float contentWidth = scrollRect.width - 16f - 4f;
            float totalH = 4f;
            foreach (var entry in entries)
            {
                CalcEntryHeight(entry, contentWidth);
                totalH += entry.CachedHeight + 6f;
            }

            Rect viewRect = new Rect(0f, 0f, contentWidth,
                Mathf.Max(totalH, scrollRect.height));
            Widgets.BeginScrollView(scrollRect, ref _chatScrollPos, viewRect);

            float curY = 4f;
            foreach (var entry in entries)
            {
                curY += DrawEntry(entry, viewRect, contentWidth, curY);
                curY += 6f;
            }

            Widgets.EndScrollView();

            if (_scrollToBottom && entries.Count > 0)
            {
                _chatScrollPos.y = Mathf.Max(0f, viewRect.height - scrollRect.height);
                _scrollToBottom = false;
            }
        }

        // ========== 右栏：工具调用卡片 ==========

        private void DrawToolPanel(Rect panelRect, List<ToolCallInfo> toolCalls)
        {
            // 标题
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, _alpha);
            string title = $"工具调用 ({toolCalls.Count})";
            Widgets.Label(new Rect(panelRect.x, panelRect.y, 120f, 16f), title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect scrollRect = new Rect(panelRect.x, panelRect.y + 14f,
                panelRect.width, panelRect.height - 14f);

            if (toolCalls.Count == 0)
            {
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.4f, 0.4f, 0.4f, _alpha);
                Widgets.Label(new Rect(scrollRect.x, scrollRect.y + 4f,
                    scrollRect.width, 16f), "暂无工具调用");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                return;
            }

            float cardWidth = scrollRect.width - 16f;
            float totalH = 4f;
            foreach (var tc in toolCalls)
                totalH += CalcCardHeight(tc, cardWidth) + 6f;

            Rect viewRect = new Rect(0f, 0f, scrollRect.width - 16f,
                Mathf.Max(totalH, scrollRect.height));
            Widgets.BeginScrollView(scrollRect, ref _toolScrollPos, viewRect);

            float curY = 4f;
            for (int i = 0; i < toolCalls.Count; i++)
            {
                curY += DrawToolCard(toolCalls[i], i, viewRect, cardWidth, curY);
                curY += 6f;
            }

            Widgets.EndScrollView();

            if (_toolScrollToBottom && toolCalls.Count > 0)
            {
                _toolScrollPos.y = Mathf.Max(0f, viewRect.height - scrollRect.height);
                _toolScrollToBottom = false;
            }
        }

        private static float CalcCardHeight(ToolCallInfo tc, float width)
        {
            string name = ToolDisplayNames.GetDisplayName(tc.Name ?? "").Replace("_", "__");
            if (string.IsNullOrEmpty(name)) name = tc.Name ?? "?";
            float headerH = Text.CalcHeight(name, width - 12f) + 6f;

            float bodyH = 0f;
            if (!string.IsNullOrEmpty(tc.Meta))
                bodyH = Text.CalcHeight(tc.Meta, width - 12f) + 4f;

            return headerH + bodyH + 10f;
        }

        private static float DrawToolCard(ToolCallInfo tc, int index, Rect viewRect, float width, float y)
        {
            string name = ToolDisplayNames.GetDisplayName(tc.Name ?? "").Replace("_", "__");
            if (string.IsNullOrEmpty(name)) name = tc.Name ?? "?";
            float headerH = Text.CalcHeight(name, width - 12f) + 6f;
            float bodyH = 0f;
            if (!string.IsNullOrEmpty(tc.Meta))
                bodyH = Text.CalcHeight(tc.Meta, width - 12f) + 4f;
            float cardH = headerH + bodyH + 10f;

            Rect cardRect = new Rect(2f, y, width, cardH);
            Widgets.DrawBoxSolid(cardRect, ToolCardBg);

            // Card header
            Rect headerRect = new Rect(cardRect.x, cardRect.y, cardRect.width, headerH + 4f);
            Widgets.DrawBoxSolid(headerRect, ToolCardHeaderBg);

            string statusIcon = tc.Status == ToolStatus.Running ? "◎"
                : tc.Status == ToolStatus.Completed ? "✔" : "✘";
            Color statusColor = tc.Status == ToolStatus.Running
                ? new Color(1f, 0.8f, 0.3f)
                : tc.Status == ToolStatus.Completed
                    ? new Color(0.3f, 1f, 0.3f)
                    : new Color(1f, 0.3f, 0.3f);

            // Index + status + name
            string headerText = $"#{index + 1} {statusIcon} {name}";
            Text.Font = GameFont.Tiny;
            GUI.color = statusColor;
            Widgets.Label(new Rect(headerRect.x + 4f, headerRect.y + 2f,
                headerRect.width - 8f, headerH), headerText);
            GUI.color = Color.white;

            // Body (meta)
            if (!string.IsNullOrEmpty(tc.Meta))
            {
                float bodyY = headerRect.yMax + 2f;
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.8f, _alpha);
                Widgets.Label(new Rect(cardRect.x + 6f, bodyY,
                    cardRect.width - 12f, bodyH), tc.Meta);
                GUI.color = Color.white;
            }

            Text.Font = GameFont.Small;
            return cardH;
        }

        // ========== 输入行 ==========

        private void DrawInputRow(Rect rect)
        {
            float btnW = 56f;
            float gap = 4f;
            float padX = 2f;

            Rect tfRect = new Rect(rect.x + padX, rect.y + 2f,
                rect.width - btnW - gap - padX * 2, rect.height - 4f);
            GUI.color = Color.white;
            GUI.SetNextControlName("chatInput");
            _inputText = Widgets.TextField(tfRect, _inputText);

            Rect sendRect = new Rect(tfRect.xMax + gap, rect.y + 2f, btnW, rect.height - 4f);
            if (Widgets.ButtonText(sendRect, "发送"))
                TrySendInput();

            GUI.color = Color.white;
        }

        // ========== 底栏 ==========

        private void DrawFooter(Rect rect)
        {
            bool connected = CCClient.IsReady;
            float btnW = 22f;
            float btnH = rect.height - 4f;
            float y = rect.y + 2f;

            // 连接状态
            float statusX = rect.x + 2f;
            Rect statusRect = new Rect(statusX, y, 70f, btnH);
            Text.Font = GameFont.Tiny;
            GUI.color = connected ? new Color(0.3f, 1f, 0.3f, _alpha) : new Color(1f, 0.4f, 0.4f, _alpha);
            Widgets.Label(statusRect, connected ? "● 已连接" : "● 未连接");
            GUI.color = Color.white;

            // 工具计数
            float toolsX = statusX + 72f;
            var toolCount = ChatDisplayState.ToolCallsSnapshot.Count;
            Rect toolsRect = new Rect(toolsX, y, 80f, btnH);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.6f, 0.6f, 0.6f, _alpha);
            Widgets.Label(toolsRect, $"Tools: {toolCount}");
            GUI.color = Color.white;

            // 透明度
            float alphaLabelX = toolsX + 75f;
            Rect alphaLabel = new Rect(alphaLabelX, y, 40f, btnH);
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.5f, 0.5f, 0.5f, _alpha);
            Widgets.Label(alphaLabel, "透明");
            GUI.color = Color.white;

            Rect alphaMinus = new Rect(alphaLabelX + 28f, y, btnW, btnH);
            if (Widgets.ButtonText(alphaMinus, "-"))
                _alpha = Mathf.Clamp(_alpha - 0.1f, 0.2f, 1f);

            Rect alphaPlus = new Rect(alphaLabelX + 28f + btnW + 2f, y, btnW, btnH);
            if (Widgets.ButtonText(alphaPlus, "+"))
                _alpha = Mathf.Clamp(_alpha + 0.1f, 0.2f, 1f);

            // 右侧按钮：清空 | 继续 | 中断
            float rightSide = rect.xMax;
            float actionBtnW = 52f;
            float actionBtnH = btnH;

            Rect abortRect = new Rect(rightSide - actionBtnW, y, actionBtnW, actionBtnH);
            GUI.color = connected ? Color.white : Color.grey;
            if (Widgets.ButtonText(abortRect, "中断"))
            {
                ChatDisplayState.MarkLastAborted();
                _ = CCClient.SendAbort();
            }

            Rect continueRect = new Rect(abortRect.x - actionBtnW - 4f, y, actionBtnW, actionBtnH);
            GUI.color = connected ? Color.white : Color.grey;
            if (Widgets.ButtonText(continueRect, "继续"))
            {
                if (connected)
                {
                    _ = CCClient.SendAbort();
                    var map = Find.CurrentMap;
                    if (map != null)
                    {
                        var colonists = PawnsFinder.AllMaps_FreeColonistsSpawned;
                        var overview = GameContextProvider.BuildColonyOverview(map, colonists, colonists.Count);
                        ChatDisplayState.AddSystemMessage(overview);
                        _ = CCClient.SendEventText("rimworld.chat", "ColonyOverview", overview);
                    }
                }
            }

            Rect clearRect = new Rect(continueRect.x - 44f - 4f, y, 44f, actionBtnH);
            GUI.color = Color.white;
            if (Widgets.ButtonText(clearRect, "清空"))
                ChatDisplayState.Clear();

            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        // ========== 对话条目 ==========

        private static void CalcEntryHeight(ChatEntry entry, float contentWidth)
        {
            var text = entry.Text ?? "";
            if (entry.State == ChatState.Done && entry.CachedHeight > 0f) return;
            if (entry.CachedHeight > 0f && text.Length == entry.CachedTextLen) return;

            float labelWidth = contentWidth - 32f;
            float bodyHeight = Text.CalcHeight(text.StripTags(), labelWidth);
            entry.CachedHeight = 25f + Mathf.Max(bodyHeight, 10f);
            entry.CachedTextLen = text.Length;
        }

        private static float DrawEntry(ChatEntry entry, Rect viewRect, float contentWidth, float y)
        {
            bool isSubagent = !string.IsNullOrEmpty(entry.AgentId);
            string label = entry.IsContext ? "系统"
                : entry.Role == ChatRole.User ? "你"
                : isSubagent ? entry.AgentType : "AI";
            string body = (entry.Text ?? "").Replace("_", "__");
            if (entry.State == ChatState.Streaming)
            {
                bool showCursor = Time.realtimeSinceStartup % 1.0f < 0.6f;
                body += showCursor ? "▌" : " ";
            }

            float bodyWidth = contentWidth - 20f;
            float bodyHeight = Mathf.Max(0f, entry.CachedHeight - 25f);
            float entryHeight = entry.CachedHeight;

            Rect bubbleRect = new Rect(2f, y, contentWidth, entryHeight);
            Color bgColor = entry.IsContext ? new Color(0.08f, 0.08f, 0.18f, 1f)
                : entry.Role == ChatRole.User ? UserBgColor
                : entry.State == ChatState.Error ? ErrorBgColor
                : isSubagent ? SubagentBgColor : AiBgColor;
            bgColor.a = _alpha;
            Widgets.DrawBoxSolid(bubbleRect, bgColor);

            // 右键复制
            if (Event.current.type == EventType.MouseDown
                && Event.current.button == 1
                && Mouse.IsOver(bubbleRect))
            {
                GUIUtility.systemCopyBuffer = entry.Text;
                Messages.Message("已复制到剪贴板", MessageTypeDefOf.SilentInput, false);
                Event.current.Use();
            }

            // 标签
            Text.Font = GameFont.Small;
            float labelW = Text.CalcSize(label).x + 4f;
            Rect labelRect = new Rect(bubbleRect.x + 6f, bubbleRect.y + 3f, labelW, 20f);
            GUI.color = entry.IsContext ? new Color(0.6f, 0.6f, 0.8f, _alpha)
                : entry.Role == ChatRole.User
                    ? new Color(0.4f, 0.8f, 1f, _alpha)
                    : isSubagent
                        ? new Color(0.8f, 0.4f, 1f, _alpha)
                        : new Color(0.4f, 1f, 0.4f, _alpha);
            Widgets.Label(labelRect, label);

            // 正文
            Rect bodyRect = new Rect(bubbleRect.x + 8f, labelRect.yMax + 2f,
                bodyWidth - 12f, Mathf.Max(bodyHeight, 10f));
            GUI.color = entry.IsContext ? new Color(0.7f, 0.7f, 0.8f, _alpha)
                : new Color(1f, 1f, 1f, _alpha);
            Text.Font = GameFont.Small;
            Widgets.Label(bodyRect, body);

            GUI.color = Color.white;
            return entryHeight;
        }
    }
}
