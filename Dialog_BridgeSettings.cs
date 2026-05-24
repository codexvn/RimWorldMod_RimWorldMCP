using UnityEngine;
using Verse;

namespace RimWorldMCP
{
    public class Dialog_BridgeSettings : Window
    {
        private McpModSettings _settings;
        private string _inputText = "";
        private string _status = "";
        private Vector2 _scrollPos;

        public Dialog_BridgeSettings(McpModSettings settings)
        {
            _settings = settings;
            doCloseButton = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            draggable = true;
            resizeable = false;
        }

        public override Vector2 InitialSize => new Vector2(500f, 350f);

        public override void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            Text.Font = GameFont.Medium;
            listing.Label("MCP 桥接消息");
            Text.Font = GameFont.Small;

            // 连接状态
            var statusLabel = McpClient.IsConnected
                ? $"已连接: {McpModSettings.BridgeTypeLabels[_settings.BridgeType]}"
                : (_settings.BridgeType > 0 ? "未连接" : "未配置");
            listing.Label(statusLabel);

            if (!string.IsNullOrEmpty(_status))
                listing.Label(_status);

            listing.Gap(12f);

            // —— 输入 ——
            listing.Label("发送消息");
            _inputText = listing.TextEntry(_inputText);
            if (listing.ButtonText("发送") && !string.IsNullOrWhiteSpace(_inputText))
            {
                _ = McpClient.SendMessage(_inputText);
                _inputText = "";
            }

            listing.End();
        }
    }
}
