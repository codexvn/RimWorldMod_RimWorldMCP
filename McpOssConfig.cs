using System;
using System.IO;
using System.Text.Json;

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

        public static void LoadFromFile(string configPath)
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    McpLog.Info($"OSS 配置文件不存在，上传已禁用: {configPath}");
                    return;
                }

                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                Enabled = TryGetBool(root, "enabled");
                ServiceUrl = TryGetString(root, "serviceUrl");
                BucketName = TryGetString(root, "bucketName");
                AccessKey = TryGetString(root, "accessKey");
                SecretKey = TryGetString(root, "secretKey");
                Region = TryGetString(root, "region");
                ForcePathStyle = TryGetBool(root, "forcePathStyle");

                McpLog.Info(IsConfigured
                    ? $"OSS 配置已加载: {ServiceUrl}/{BucketName}"
                    : "OSS 配置不完整，上传已禁用");
            }
            catch (Exception ex)
            {
                McpLog.Warn($"加载 OSS 配置失败: {ex.Message}");
                Enabled = false;
            }
        }

        private static string TryGetString(JsonElement root, string key)
        {
            return root.TryGetProperty(key, out var prop) ? prop.GetString() ?? "" : "";
        }

        private static bool TryGetBool(JsonElement root, string key)
        {
            return root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.True;
        }
    }
}
