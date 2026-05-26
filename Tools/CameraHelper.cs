using System.Threading.Tasks;
using Verse;

namespace RimWorldMCP.Tools
{
    /// <summary>视角移动辅助 — 抽象移动画面方法，支持传坐标和等待时间</summary>
    public static class CameraHelper
    {
        /// <summary>移动到指定坐标</summary>
        public static async Task MoveTo(int posX, int posY, int waitMs = 300)
        {
            await McpCommandQueue.DispatchAsync<object?>(() =>
            {
                var map = Find.CurrentMap;
                if (map == null) return null;
                var cell = new IntVec3(posX, 0, posY);
                Find.CameraDriver.PanToMapLoc(cell);
                return null;
            });

            if (waitMs > 0)
                await Task.Delay(waitMs);
        }
    }
}
