using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimWorldMCP.Tools
{
    public static class CameraHelper
    {
        /// <summary>舒适的默认视野大小（中等距离）</summary>
        private const float ComfortableSize = 30f;

        /// <summary>拉远阈值：需要超过当前视野 30% 才拉远</summary>
        private const float ZoomOutThreshold = 1.3f;

        /// <summary>视野过大阈值：当前视野超过需求 50% 且过大时，回弹到舒适距离</summary>
        private const float ZoomReturnIfOversized = 50f;

        /// <summary>回弹目标需求上限：只有小区域才触发回弹</summary>
        private const float ZoomReturnNeedMax = 30f;

        /// <summary>移动到指定区域中心，智能缩放：
        /// 1. 只拉远不拉近（避免拉近后看不见其他东西）
        /// 2. 拉远需要阈值 30%（避免频繁微调）
        /// 3. 视野过大时渐进回弹到舒适距离</summary>
        public static async Task MoveToRange(int minX, int minZ, int maxX, int maxZ)
        {
            await McpCommandQueue.DispatchAsync<object?>(() =>
            {
                var map = Find.CurrentMap;
                if (map == null) return null;

                var driver = Find.CameraDriver;
                float aspect = (float)UI.screenWidth / (float)UI.screenHeight;

                // 1. 计算区域中心和所需视野
                float centerX = (minX + maxX + 1) / 2f;
                float centerZ = (minZ + maxZ + 1) / 2f;
                float rectW = (maxX - minX + 1);
                float rectH = (maxZ - minZ + 1);

                float needSizeW = (rectW * 1.1f) / (2f * aspect);
                float needSizeH = (rectH * 1.1f) / 2f;
                float needSize = Mathf.Max(needSizeW, needSizeH);
                needSize = Mathf.Clamp(needSize, driver.config.sizeRange.min, driver.config.sizeRange.max);

                float curSize = driver.RootSize;
                float targetSize;

                if (needSize > curSize * ZoomOutThreshold)
                {
                    // 矩形明显超出当前视野 → 拉远
                    targetSize = needSize;
                }
                else if (curSize > ZoomReturnIfOversized && needSize < ZoomReturnNeedMax)
                {
                    // 视野太远 + 目标区域很小 → 回弹到舒适距离（但不小于所需）
                    targetSize = Mathf.Max(needSize, ComfortableSize);
                }
                else
                {
                    // 当前缩放合适 → 只平移不缩放
                    targetSize = curSize;
                }

                // 2. 构建目标位置并动画
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
