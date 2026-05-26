using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Verse;

namespace RimWorldMCP.Tools
{
    public class Tool_SetGameSpeed : ITool
    {
        public string Name => "set_game_speed";

        public string Description => "设置游戏速度（暂停/1×/3×/6×）或查询当前速度。Set game speed (pause/1×/3×/6×) or query current speed.";

        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                speed = new
                {
                    type = "string",
                    description = "目标速度。Target speed: pause, normal, fast, superfast。不传则只查询当前速度。",
                    @enum = new[] { "pause", "normal", "fast", "superfast" }
                }
            },
            required = new string[] { }
        });

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            return await McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    var tm = Find.TickManager;
                    if (tm == null) return ToolResult.Error("TickManager 不可用。TickManager is not available.");

                    string? speedStr = null;
                    if (args != null && args.Value.TryGetProperty("speed", out var s))
                        speedStr = s.GetString();

                    // 如果传了 speed 参数，执行速度设置
                    if (!string.IsNullOrEmpty(speedStr))
                    {
                        var targetSpeed = speedStr switch
                        {
                            "pause" => TimeSpeed.Paused,
                            "normal" => TimeSpeed.Normal,
                            "fast" => TimeSpeed.Fast,
                            "superfast" => TimeSpeed.Superfast,
                            _ => (TimeSpeed?)null
                        };

                        if (targetSpeed == null)
                            return ToolResult.Error($"无效速度: {speedStr}。可选: pause, normal, fast, superfast。Invalid speed.");

                        tm.CurTimeSpeed = targetSpeed.Value;
                    }

                    // 构建当前速度状态报告
                    var sb = new StringBuilder();
                    var currentSpeed = tm.CurTimeSpeed;
                    var speedName = currentSpeed switch
                    {
                        TimeSpeed.Paused => "暂停 (0×)",
                        TimeSpeed.Normal => "普通 (1×)",
                        TimeSpeed.Fast => "快速 (3×)",
                        TimeSpeed.Superfast => "极快 (6×)",
                        TimeSpeed.Ultrafast => "超快 (15×)",
                        _ => "未知"
                    };

                    var paused = tm.Paused;
                    var forcedPause = tm.ForcePaused;
                    var forcedNormal = tm.slower.ForcedNormalSpeed;
                    var rate = tm.TickRateMultiplier;

                    if (!string.IsNullOrEmpty(speedStr))
                        sb.AppendLine($"游戏速度已设置为: {speedName}");
                    else
                        sb.AppendLine($"当前游戏速度: {speedName}");

                    sb.AppendLine($"实际倍率: {rate:F1}×");
                    sb.AppendLine($"暂停状态: {(paused ? "已暂停" : "运行中")}");

                    if (forcedPause)
                    {
                        sb.AppendLine();
                        sb.AppendLine("## 强制暂停原因");
                        var ws = Find.WindowStack;
                        if (ws != null)
                        {
                            for (int i = 0; i < ws.Count; i++)
                            {
                                var w = ws[i];
                                if (w.forcePause)
                                    sb.AppendLine($"  - 窗口: {w.GetType().Name}（已锁定暂停）");
                            }
                        }
                        if (LongEventHandler.ForcePause)
                            sb.AppendLine("  - 正在处理长事件");
                        if (Find.TilePicker?.Active == true)
                            sb.AppendLine("  - 地块选择器激活中");
                    }

                    if (forcedNormal)
                    {
                        sb.AppendLine();
                        sb.AppendLine("⚠ 速度被强制限制为 1×（威胁事件或地图生成中），设定更高速度暂时无效。");
                    }

                    return ToolResult.Success(sb.ToString().TrimEnd());
                }
                catch (System.Exception ex)
                {
                    return ToolResult.Error($"设置游戏速度失败: {ex.Message}. Failed to set game speed.");
                }
            });
        }

        public (int x, int y)? GetTargetPos(JsonElement? args) => null;
    }
}
