using Verse;

namespace RimWorldMCP
{
    public enum TokenBudgetExceedAction { Block, Warn }

    public class McpModSettings : ModSettings
    {
        // 调试
        public LogLevel LogLevel = LogLevel.Info;

        // MCP 服务器
        public string McpHost = "0.0.0.0";
        public int McpPort = 9877;

        // CC 桥接
        public string CCBHost = "127.0.0.1";
        public int CCBPort = 19999;
        public string CCBAuthToken = "";
        public bool CCBAutoStart = true;
        public string CCBModelName = "";

        // CC 项目设置 JSON 模板
        public string CCBProjectSettingsJson = "";

        // 工具行为
        public bool AutoMoveCamera = true;

        // OSS
        public bool OssEnabled = false;
        public string OssServiceUrl = "";
        public string OssBucketName = "";
        public string OssAccessKey = "";
        public string OssSecretKey = "";
        public bool OssUseSignedUrl = true;
        public int OssSignedUrlExpiryHours = 24;

        // Token 预算
        public long TokenBudgetLimit = 0;
        public TokenBudgetExceedAction TokenBudgetExceedAction = TokenBudgetExceedAction.Block;
        public string TokenBudgetWebhookUrl = "";

        public static readonly string[] LogLevelLabels = { "Debug", "Info", "Warn", "Error" };
        public static readonly string[] BudgetActionLabels = { "暂停游戏并阻止", "仅警告通知" };

        public override void ExposeData()
        {
            base.ExposeData();
            var logLevelInt = (int)LogLevel;
            Scribe_Values.Look(ref McpHost, "mcpHost", "0.0.0.0");
            Scribe_Values.Look(ref McpPort, "mcpPort", 9877);
            Scribe_Values.Look(ref logLevelInt, "logLevel", (int)LogLevel.Info);
            LogLevel = (LogLevel)logLevelInt;
            Scribe_Values.Look(ref CCBHost, "ccbHost", "127.0.0.1");
            Scribe_Values.Look(ref CCBPort, "ccbPort", 19999);
            Scribe_Values.Look(ref CCBAuthToken, "ccbAuthToken", "");
            Scribe_Values.Look(ref CCBAutoStart, "ccbAutoStart", true);
            Scribe_Values.Look(ref CCBModelName, "ccbModelName", "");
            var defaultProjectSettingsJson = BridgeLifecycle.BuildProjectSettingsJson(9877);
            Scribe_Values.Look(ref CCBProjectSettingsJson, "ccbProjectSettingsJson", defaultProjectSettingsJson);
            Scribe_Values.Look(ref AutoMoveCamera, "autoMoveCamera", true);
            Scribe_Values.Look(ref OssEnabled, "ossEnabled", false);
            Scribe_Values.Look(ref OssServiceUrl, "ossServiceUrl", "");
            Scribe_Values.Look(ref OssBucketName, "ossBucketName", "");
            Scribe_Values.Look(ref OssAccessKey, "ossAccessKey", "");
            Scribe_Values.Look(ref OssSecretKey, "ossSecretKey", "");
            Scribe_Values.Look(ref OssUseSignedUrl, "ossUseSignedUrl", true);
            Scribe_Values.Look(ref OssSignedUrlExpiryHours, "ossSignedUrlExpiryHours", 24);
            var budgetAction = (int)TokenBudgetExceedAction;
            Scribe_Values.Look(ref TokenBudgetLimit, "tokenBudgetLimit", 0L);
            Scribe_Values.Look(ref budgetAction, "tokenBudgetAction", (int)TokenBudgetExceedAction.Block);
            TokenBudgetExceedAction = (TokenBudgetExceedAction)budgetAction;
            Scribe_Values.Look(ref TokenBudgetWebhookUrl, "tokenBudgetWebhookUrl", "");
        }
    }
}
