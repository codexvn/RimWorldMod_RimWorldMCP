using System;

namespace RimWorldMCP
{
    public static class McpOssConfig
    {
        public static bool Enabled { get; set; }
        public static string ServiceUrl { get; set; } = "";
        public static string BucketName { get; set; } = "";
        public static string AccessKey { get; set; } = "";
        public static string SecretKey { get; set; } = "";
        public static string Region { get; set; } = "";
        public static bool ForcePathStyle { get; set; }

        public static bool IsConfigured =>
            Enabled &&
            !string.IsNullOrEmpty(ServiceUrl) &&
            !string.IsNullOrEmpty(BucketName) &&
            !string.IsNullOrEmpty(AccessKey) &&
            !string.IsNullOrEmpty(SecretKey);

        /// <summary>从 RimWorld ModSettings 实例同步配置</summary>
        public static void LoadFromModSettings(McpModSettings settings)
        {
            if (settings == null) return;

            Enabled = settings.OssEnabled;
            ServiceUrl = settings.OssServiceUrl ?? "";
            BucketName = settings.OssBucketName ?? "";
            AccessKey = settings.OssAccessKey ?? "";
            SecretKey = settings.OssSecretKey ?? "";
            Region = settings.OssRegion ?? "";
            ForcePathStyle = settings.OssForcePathStyle;

            McpLog.Info(IsConfigured
                ? $"OSS 配置已加载: {ServiceUrl}/{BucketName}"
                : "OSS 未配置或未启用");
        }
    }
}
