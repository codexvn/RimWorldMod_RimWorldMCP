using UnityEngine;
using Verse;

namespace RimWorldMCP
{
    /// <summary>右下角 AI 对话开关按钮 + 工具调用状态文字</summary>
    public class MapComponent_McpUI : MapComponent
    {
        public MapComponent_McpUI(Map map) : base(map) { }

        public override void MapComponentOnGUI()
        {
            base.MapComponentOnGUI();
            if (Find.CurrentMap == null) return;

            // 放在时间控件上方，对齐原版 ToggleableIcon 大小
            float btnSize = 24f;
            float x = UI.screenWidth - 162f;
            float y = UI.screenHeight - 72f;
            Rect btnRect = new Rect(x, y, btnSize, btnSize);

            bool isOpen = Find.WindowStack.IsOpen<Dialog_AiChat>();
            bool streaming = ChatDisplayState.Snapshot.Count > 0
                && ChatDisplayState.Snapshot[ChatDisplayState.Snapshot.Count - 1].State == ChatState.Streaming;

            Color origColor = GUI.color;
            if (streaming)
                GUI.color = Time.realtimeSinceStartup % 1.0f < 0.5f ? Color.cyan : Color.white;
            else if (!GatewayClient.IsConnected)
                GUI.color = Color.grey;
            else if (isOpen)
                GUI.color = Color.cyan;

            if (Widgets.ButtonImage(btnRect, TexButton.Info))
            {
                if (isOpen)
                    Find.WindowStack.WindowOfType<Dialog_AiChat>()?.Close();
                else
                    Find.WindowStack.Add(new Dialog_AiChat());
            }
            GUI.color = origColor;

            if (isOpen)
            {
                Rect markerRect = new Rect(btnRect.xMax - 6f, btnRect.yMax - 6f, 4f, 4f);
                Widgets.DrawBoxSolid(markerRect, Color.green);
            }

            // 工具调用状态文字（图标按钮左侧）
            DrawToolCallLabel(btnRect);
        }

        private static void DrawToolCallLabel(Rect btnRect)
        {
            var toolCalls = ChatDisplayState.ToolCallsSnapshot;
            if (toolCalls.Count == 0) return;

            // 只显示最近 2 个运行中的工具调用
            var active = new System.Collections.Generic.List<ToolCallInfo>();
            for (int i = toolCalls.Count - 1; i >= 0 && active.Count < 2; i--)
            {
                if (toolCalls[i].Status == ToolStatus.Running)
                    active.Add(toolCalls[i]);
            }

            if (active.Count == 0) return;

            float labelX = btnRect.x - 260f;
            float labelY = btnRect.y;
            float labelW = 250f;

            for (int i = 0; i < active.Count; i++)
            {
                var tc = active[i];
                // 格式: "调用工具: xxx (参数)"
                string line = !string.IsNullOrEmpty(tc.Meta)
                    ? $"{tc.Title} ({tc.Meta})"
                    : tc.Title;
                if (string.IsNullOrEmpty(line)) continue;

                float rowY = labelY + i * 18f;

                // 文字阴影增强可读性
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0, 0, 0, 0.6f);
                Widgets.Label(new Rect(labelX + 1f, rowY + 1f, labelW, 18f), line);
                GUI.color = Color.yellow;
                Widgets.Label(new Rect(labelX, rowY, labelW, 18f), line);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }
        }
    }
}
