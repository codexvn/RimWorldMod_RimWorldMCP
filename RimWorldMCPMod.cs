using System.Linq;
using UnityEngine;
using Verse;
using RimWorldMCP.Tools;

namespace RimWorldMCP
{
    public class RimWorldMCPMod : Mod
    {
        public static RimWorldMCPMod Instance { get; private set; } = null!;
        public McpModSettings Settings { get; private set; }
        private Vector2 _scrollPos;
        private Vector2 _jsonScrollPos;

        public RimWorldMCPMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<McpModSettings>();
            McpLog.MinLogLevel = Settings.LogLevel;
            GlobalModelUsageStore.Load();
        }

        public override string SettingsCategory()
        {
            return "RimWorld MCP";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            float h = 830f;
            h += Settings.CCBAutoStart ? 70f : 170f;
            // Token 预算段
            h += 120f;
            if (Settings.TokenBudgetExceedAction == TokenBudgetExceedAction.Warn) h += 70f;
            // 全局用量汇总表
            var globalModels = GlobalModelUsageStore.AllModels;
            h += 60f + globalModels.Count * 22f;
            if (Settings.OssEnabled) h += 220f;
            if (Settings.OssEnabled && Settings.OssUseSignedUrl) h += 50f;

            Rect viewRect = new Rect(0f, 0f, inRect.width - 16f, h);
            Widgets.BeginScrollView(inRect, ref _scrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // ====== 日志级别 ======
            listing.Label($"日志级别: {McpModSettings.LogLevelLabels[(int)Settings.LogLevel]}");
            if (listing.ButtonText("切换"))
            {
                var next = (int)Settings.LogLevel + 1;
                if (next >= McpModSettings.LogLevelLabels.Length) next = 0;
                Settings.LogLevel = (LogLevel)next;
                McpLog.MinLogLevel = Settings.LogLevel;
            }
            listing.Gap(24f);

            // ====== 工具行为 ======
            listing.Label("工具行为");
            listing.Gap(2f);
            var cameraTools = ToolRegistry.CameraToolNames;
            var tooltip = "AI 调用带坐标参数的工具时，自动将游戏视角平滑移动到目标位置。"
                + (cameraTools.Count > 0
                    ? $"\n\n支持自动移动的工具（{cameraTools.Count} 个）：\n" + string.Join(", ", cameraTools)
                    : "");
            listing.CheckboxLabeled("调用工具时自动移动视角", ref Settings.AutoMoveCamera, tooltip);
            listing.Gap(24f);

            // ====== MCP 服务器 ======
            listing.Label("MCP 服务器");
            listing.Gap(2f);

            listing.Label("监听地址 (localhost / 0.0.0.0 / 内网 IP)");
            Settings.McpHost = listing.TextEntry(Settings.McpHost);

            listing.Label("端口");
            var portStr = listing.TextEntry(Settings.McpPort.ToString());
            if (int.TryParse(portStr, out int port) && port > 0 && port <= 65535)
                Settings.McpPort = port;

            listing.Gap(24f);

            // ====== CC 桥接 ======
            listing.Label("CC 桥接");
            listing.Gap(2f);

            listing.Label("连接地址 (WebSocket)");
            listing.Label("ws://{host}:{port}，默认连本地 Companion");
            Settings.CCBHost = listing.TextEntry(Settings.CCBHost);
            var ccPortStr = listing.TextEntry(Settings.CCBPort.ToString());
            if (int.TryParse(ccPortStr, out int ccPort) && ccPort > 0 && ccPort <= 65535)
                Settings.CCBPort = ccPort;

            listing.Gap(6f);
            listing.CheckboxLabeled("自动启动本地 Companion", ref Settings.CCBAutoStart,
                "开启后，游戏加载时自动 spawn Node.js 子进程。");

            if (Settings.CCBAutoStart)
            {
                listing.Label("Token (可选)");
                Settings.CCBAuthToken = listing.TextEntry(Settings.CCBAuthToken);
                listing.Label("模型 (留空用 settings.json)");
                Settings.CCBModelName = listing.TextEntry(Settings.CCBModelName);
            }

            listing.Gap(12f);
            listing.Label("项目设置 JSON 模板（写出为 .claude/settings.json）");
            listing.Gap(2f);
            var textAreaRect = listing.GetRect(180f);
            var textContent = new GUIContent(Settings.CCBProjectSettingsJson);
            var textHeight = GUI.skin.textArea.CalcHeight(textContent, textAreaRect.width);
            var jsonViewRect = new Rect(0f, 0f, textAreaRect.width - 16f, Mathf.Max(textHeight, 180f));
            Widgets.BeginScrollView(textAreaRect, ref _jsonScrollPos, jsonViewRect);
            Settings.CCBProjectSettingsJson = GUI.TextArea(jsonViewRect, Settings.CCBProjectSettingsJson);
            Widgets.EndScrollView();
            listing.Gap(6f);
            if (listing.ButtonText("复位为默认值"))
            {
                Settings.CCBProjectSettingsJson = BridgeLifecycle.BuildProjectSettingsJson(Settings.McpPort);
            }
            var installed = BridgeLifecycle.IsCompanionInstalled();
            var installing = BridgeLifecycle.IsInstalling;
            var status = BridgeLifecycle.InstallStatus;

            if (installing)
            {
                listing.Label("安装中...");
                if (!string.IsNullOrEmpty(status))
                    listing.Label($"  {status}");
            }
            else if (installed)
            {
                listing.Label("Claude Code 状态: 已安装");
                if (listing.ButtonText("重新安装"))
                    BridgeLifecycle.InstallCompanion();
                if (listing.ButtonText("卸载 Claude Code 依赖"))
                    BridgeLifecycle.UninstallCompanion();
            }
            else
            {
                listing.Label($"Companion 状态: 未安装{(string.IsNullOrEmpty(status) ? "" : $" ({status})")}");
                if (!installing && listing.ButtonText("安装 Claude Code 依赖"))
                    BridgeLifecycle.InstallCompanion();
            }

            listing.Gap(24f);

            // ====== Token 预算 ======
            listing.Label("Token 预算");
            listing.Gap(2f);

            listing.Label("预算上限 (0=无限制)");
            var limitStr = listing.TextEntry(Settings.TokenBudgetLimit > 0
                ? (Settings.TokenBudgetLimit / 1_000_000f).ToString("F1") + "M"
                : "0");
            if (limitStr == "0" || limitStr == "0M")
            {
                Settings.TokenBudgetLimit = 0;
            }
            else if (limitStr.EndsWith("M") || limitStr.EndsWith("m"))
            {
                if (float.TryParse(limitStr.TrimEnd('M', 'm'), out float mVal) && mVal > 0)
                    Settings.TokenBudgetLimit = (long)(mVal * 1_000_000);
            }
            else if (long.TryParse(limitStr, out long rawVal))
            {
                Settings.TokenBudgetLimit = rawVal;
            }
            listing.Gap(4f);

            // 超出行为
            var actionLabels = McpModSettings.BudgetActionLabels;
            int actionIdx = (int)Settings.TokenBudgetExceedAction;
            listing.Label($"超出行为: {actionLabels[actionIdx]}");
            if (listing.ButtonText("切换"))
            {
                Settings.TokenBudgetExceedAction = actionIdx == 0
                    ? TokenBudgetExceedAction.Warn
                    : TokenBudgetExceedAction.Block;
            }
            listing.Gap(4f);

            // Webhook URL (Warn 模式)
            if (Settings.TokenBudgetExceedAction == TokenBudgetExceedAction.Warn)
            {
                listing.Label("Webhook URL (超出时 POST 通知)");
                Settings.TokenBudgetWebhookUrl = listing.TextEntry(Settings.TokenBudgetWebhookUrl);
            }

            listing.Gap(24f);

            // ====== 全局用量汇总 ======
            listing.Label("全局用量汇总（所有存档）");
            listing.Gap(2f);

            if (globalModels.Count == 0)
            {
                listing.Label("  暂无记录");
            }
            else
            {
                // 表头
                var headerRect = listing.GetRect(20f);
                Text.Font = GameFont.Tiny;
                float[] colX = { headerRect.x, headerRect.x + 140f, headerRect.x + 210f, headerRect.x + 280f,
                    headerRect.x + 350f, headerRect.x + 400f, headerRect.x + 460f };
                GUI.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                Widgets.Label(new Rect(colX[0], headerRect.y, 130f, 20f), "模型");
                Widgets.Label(new Rect(colX[1], headerRect.y, 65f, 20f), "输入");
                Widgets.Label(new Rect(colX[2], headerRect.y, 65f, 20f), "输出");
                Widgets.Label(new Rect(colX[3], headerRect.y, 65f, 20f), "缓存命中");
                Widgets.Label(new Rect(colX[4], headerRect.y, 45f, 20f), "请求");
                Widgets.Label(new Rect(colX[5], headerRect.y, 55f, 20f), "合计");
                Widgets.Label(new Rect(colX[6], headerRect.y, 50f, 20f), "占比");
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                listing.Gap(4f);

                long grandTotal = 0;
                foreach (var kv in globalModels) grandTotal += kv.Value.TotalTokens;

                string fmt(long v) => v >= 1_000_000 ? $"{v / 1_000_000f:F1}M" :
                                      v >= 1_000 ? $"{v / 1_000f:F0}K" : v.ToString();

                foreach (var kv in globalModels.OrderByDescending(kv => kv.Value.TotalTokens))
                {
                    var d = kv.Value;
                    double pct = grandTotal > 0 ? (double)d.TotalTokens / grandTotal * 100.0 : 0;
                    var rowRect = listing.GetRect(20f);
                    Text.Font = GameFont.Tiny;
                    Widgets.Label(new Rect(colX[0], rowRect.y, 130f, 20f), kv.Key);
                    Widgets.Label(new Rect(colX[1], rowRect.y, 65f, 20f), fmt(d.InputTokens));
                    Widgets.Label(new Rect(colX[2], rowRect.y, 65f, 20f), fmt(d.OutputTokens));
                    Widgets.Label(new Rect(colX[3], rowRect.y, 65f, 20f), fmt(d.CacheReadTokens));
                    Widgets.Label(new Rect(colX[4], rowRect.y, 45f, 20f), d.RequestCount.ToString());
                    Widgets.Label(new Rect(colX[5], rowRect.y, 55f, 20f), fmt(d.TotalTokens));
                    Widgets.Label(new Rect(colX[6], rowRect.y, 50f, 20f), $"{pct:F0}%");
                    Text.Font = GameFont.Small;
                }

                // 合计行
                listing.Gap(2f);
                var totalRect = listing.GetRect(20f);
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.7f, 0.7f, 0.7f, 1f);
                Widgets.Label(new Rect(colX[0], totalRect.y, 130f, 20f), "合计");
                Widgets.Label(new Rect(colX[5], totalRect.y, 55f, 20f), fmt(grandTotal));
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
            }

            listing.Gap(12f);
            if (globalModels.Count > 0)
            {
                if (listing.ButtonText("清空全部统计"))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "确认清空全部 Token 统计？此操作不可撤销。",
                        GlobalModelUsageStore.Clear, true));
                }
            }

            listing.Gap(24f);

            // ====== OSS ======
            listing.CheckboxLabeled("启用 OSS 上传", ref Settings.OssEnabled,
                "开启后，截图将自动上传到阿里云 OSS");

            if (Settings.OssEnabled)
            {
                listing.Gap(12f);

                listing.Label("Endpoint");
                listing.Label("示例: https://oss-cn-beijing.aliyuncs.com");
                Settings.OssServiceUrl = listing.TextEntry(Settings.OssServiceUrl);

                listing.Label("Bucket 名称");
                Settings.OssBucketName = listing.TextEntry(Settings.OssBucketName);

                listing.Label("AccessKey ID");
                Settings.OssAccessKey = listing.TextEntry(Settings.OssAccessKey);

                listing.Label("AccessKey Secret");
                Settings.OssSecretKey = listing.TextEntry(Settings.OssSecretKey);

                listing.Gap(12f);
                listing.CheckboxLabeled("使用签名 URL", ref Settings.OssUseSignedUrl,
                    "生成有时效的预签名 URL，Bucket 无需设为公开读。关闭则返回公开 URL。");

                if (Settings.OssUseSignedUrl)
                {
                    listing.Label("签名有效期（小时）");
                    var expiryStr = listing.TextEntry(Settings.OssSignedUrlExpiryHours.ToString());
                    if (int.TryParse(expiryStr, out int expiryHours) && expiryHours > 0)
                        Settings.OssSignedUrlExpiryHours = expiryHours;
                }
            }

            listing.End();
            Widgets.EndScrollView();
        }
    }
}
