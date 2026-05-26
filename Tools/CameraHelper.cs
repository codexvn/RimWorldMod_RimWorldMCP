using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RimWorldMCP.Tools
{
    public static class CameraHelper
    {
        /// <summary>移动到指定区域中心，必要时缩放以包含整个矩形</summary>
        public static async Task MoveToRange(int minX, int minZ, int maxX, int maxZ)
        {
            await McpCommandQueue.DispatchAsync<object?>(() =>
            {
                var map = Find.CurrentMap;
                if (map == null) return null;

                var driver = Find.CameraDriver;
                float aspect = (float)UI.screenWidth / (float)UI.screenHeight;

                // 1. 计算区域中心和尺寸
                float centerX = (minX + maxX + 1) / 2f;
                float centerZ = (minZ + maxZ + 1) / 2f;
                float rectW = (maxX - minX + 1); // 单元格宽度
                float rectH = (maxZ - minZ + 1); // 单元格高度

                // 2. 计算覆盖整个矩形需要的 RootSize（加 10% 边距）
                float needSizeW = (rectW * 1.1f) / (2f * aspect);
                float needSizeH = (rectH * 1.1f) / 2f;
                float needSize = Mathf.Max(needSizeW, needSizeH);

                // 3. 限制在允许的缩放范围内
                needSize = Mathf.Clamp(needSize, driver.config.sizeRange.min, driver.config.sizeRange.max);

                // 4. 当前 RootSize 已经覆盖 → 只平移，不缩放
                float targetSize = (driver.RootSize >= needSize) ? driver.RootSize : needSize;

                // 5. 构建目标位置
                float targetY = 15f + (targetSize - driver.config.sizeRange.min)
                    / (driver.config.sizeRange.max - driver.config.sizeRange.min) * 50f;
                var targetLoc = new Vector3(centerX, targetY, centerZ);

                // 6. 动画平移+缩放
                driver.PanToMapLocAndSize(targetLoc, targetSize, 0.35f);

                return null;
            });

            await Task.Delay(400); // 等待动画完成
        }
    }
}
