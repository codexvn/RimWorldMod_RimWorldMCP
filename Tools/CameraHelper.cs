using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimWorldMCP.Tools
{
    public static class CameraHelper
    {
        private const float ComfortableSize = 28f;
        private const float ZoomOutThreshold = 1.3f;
        private const float MaxZoomOut = 40f;

        /// <summary>移动到区域中心，智能双向缩放：
        /// 超出视野 30% → 拉远（上限 40 格）
        /// 视野远大于需求 → 拉近回舒适距离
        /// 否则 → 只平移</summary>
        public static async Task MoveToRange(int minX, int minZ, int maxX, int maxZ)
        {
            await McpCommandQueue.DispatchAsync<object?>(() =>
            {
                var map = Find.CurrentMap;
                if (map == null) return null;

                var driver = Find.CameraDriver;
                float aspect = (float)UI.screenWidth / (float)UI.screenHeight;

                float centerX = (minX + maxX + 1) / 2f;
                float centerZ = (minZ + maxZ + 1) / 2f;
                float rectW = (maxX - minX + 1);
                float rectH = (maxZ - minZ + 1);

                float needSizeW = (rectW * 1.05f) / (2f * aspect);
                float needSizeH = (rectH * 1.05f) / 2f;
                float needSize = Mathf.Max(needSizeW, needSizeH);
                needSize = Mathf.Clamp(needSize, driver.config.sizeRange.min, driver.config.sizeRange.max);

                float curSize = driver.RootSize;
                float targetSize;

                if (needSize > curSize * ZoomOutThreshold)
                {
                    // 矩形明显超出 → 拉远（有上限）
                    targetSize = Mathf.Min(needSize, MaxZoomOut);
                }
                else if (needSize < curSize * 0.7f && curSize > ComfortableSize + 2f)
                {
                    // 视野太远，目标太小 → 拉近回舒适距离
                    targetSize = Mathf.Max(needSize, ComfortableSize);
                }
                else
                {
                    // 缩放合适 → 不调
                    targetSize = curSize;
                }

                float targetY = 15f + (targetSize - driver.config.sizeRange.min)
                    / (driver.config.sizeRange.max - driver.config.sizeRange.min) * 50f;
                var targetLoc = new Vector3(centerX, targetY, centerZ);

                driver.PanToMapLocAndSize(targetLoc, targetSize, 0.35f);

                return null;
            });

            await Task.Delay(400);
        }
    }
}
