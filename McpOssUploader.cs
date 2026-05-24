using System;
using System.Collections.Concurrent;
using System.IO;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;

namespace RimWorldMCP
{
    public static class McpOssUploader
    {
        private static readonly ConcurrentQueue<(string filePath, string objectKey)> _pending = new();

        public static void EnqueuePendingUpload(string filePath, string objectKey)
        {
            _pending.Enqueue((filePath, objectKey));
        }

        /// <summary>主线程每帧调用，处理待上传队列（仅在配置完成时执行）</summary>
        public static void ProcessPendingUploads()
        {
            if (!McpOssConfig.IsConfigured || _pending.IsEmpty) return;

            while (_pending.TryDequeue(out var item))
            {
                try
                {
                    UploadInternal(item.filePath, item.objectKey);
                }
                catch (Exception ex)
                {
                    McpLog.Error($"OSS 上传失败 ({item.objectKey}): {ex.Message}");
                }
            }
        }

        private static void UploadInternal(string filePath, string objectKey)
        {
            if (!File.Exists(filePath))
            {
                McpLog.Warn($"OSS 待上传文件尚不存在，跳过: {filePath}");
                return;
            }

            var s3Config = new AmazonS3Config
            {
                ServiceURL = McpOssConfig.ServiceUrl,
                ForcePathStyle = McpOssConfig.ForcePathStyle,
                RegionEndpoint = RegionEndpoint.GetBySystemName(McpOssConfig.Region)
            };

            using var client = new AmazonS3Client(McpOssConfig.AccessKey, McpOssConfig.SecretKey, s3Config);
            client.PutObject(new PutObjectRequest
            {
                BucketName = McpOssConfig.BucketName,
                Key = objectKey,
                FilePath = filePath,
                ContentType = "image/png"
            });

            McpLog.Info($"OSS 上传成功: {objectKey}");

            try { File.Delete(filePath); }
            catch (Exception ex) { McpLog.Warn($"删除临时截图失败: {ex.Message}"); }
        }

        public static string GetPublicUrl(string objectKey)
        {
            return McpOssConfig.ForcePathStyle
                ? $"{McpOssConfig.ServiceUrl}/{McpOssConfig.BucketName}/{objectKey}"
                : $"https://{McpOssConfig.BucketName}.{new Uri(McpOssConfig.ServiceUrl).Host}/{objectKey}";
        }
    }
}
