using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_DesignateRoom : ITool
    {
        public string Name => "designate_room";
        public string Description => "快速建造一个矩形房间（自动放置四面墙）。尺寸不包括墙体本身（内部空间）。例如 13x13 的房间会建造 15x15 的外墙范围。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                center_x = new { type = "integer", description = "房间中心的 X 坐标" },
                center_y = new { type = "integer", description = "房间中心的 Y 坐标" },
                center_z = new { type = "integer", description = "房间中心的 Z 坐标" },
                width = new { type = "integer", description = "房间内部宽度（不含墙），默认 13", @default = 13 },
                height = new { type = "integer", description = "房间内部高度（不含墙），默认 13", @default = 13 },
                wall_defName = new { type = "string", description = "墙体材料 defName，默认 Steel", @default = "Steel" },
                door_positions = new { type = "string", description = "门的位置，多个用逗号分隔。可选: top, bottom, left, right, center_top, center_bottom, center_left, center_right" },
                door_defName = new { type = "string", description = "门的 defName，默认 Door", @default = "Door" },
                floor_defName = new { type = "string", description = "地板 defName，可选" }
            },
            required = new[] { "center_x", "center_y", "center_z" }
        });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null) return Task.FromResult(ToolResult.Error("缺少参数"));
            if (!args.Value.TryGetProperty("center_x", out var cx) || !cx.TryGetInt32(out var centerX)) return Task.FromResult(ToolResult.Error("缺少 center_x"));
            if (!args.Value.TryGetProperty("center_y", out var cy) || !cy.TryGetInt32(out var centerY)) return Task.FromResult(ToolResult.Error("缺少 center_y"));
            if (!args.Value.TryGetProperty("center_z", out var cz) || !cz.TryGetInt32(out var centerZ)) return Task.FromResult(ToolResult.Error("缺少 center_z"));

            var width = 13; var height = 13;
            if (args.Value.TryGetProperty("width", out var w) && w.TryGetInt32(out var wv)) width = wv;
            if (args.Value.TryGetProperty("height", out var h) && h.TryGetInt32(out var hv)) height = hv;
            var wallDef = "Steel";
            if (args.Value.TryGetProperty("wall_defName", out var wd)) wallDef = wd.GetString() ?? "Steel";
            var doors = "";
            if (args.Value.TryGetProperty("door_positions", out var dp)) doors = dp.GetString() ?? "";
            var doorDef = "Door";
            if (args.Value.TryGetProperty("door_defName", out var dd)) doorDef = dd.GetString() ?? "Door";
            var floorDef = "";
            if (args.Value.TryGetProperty("floor_defName", out var fd)) floorDef = fd.GetString() ?? "";

            var roomW = width + 2; var roomH = height + 2;
            var startX = centerX - roomW / 2; var startY = centerY - roomH / 2;
            var endX = startX + roomW - 1; var endY = startY + roomH - 1;
            var wallCount = roomW * 2 + roomH * 2 - 4;

            var doorList = string.IsNullOrEmpty(doors) ? Array.Empty<string>() : doors.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var doorPositions = new List<(int x, int y)>();
            foreach (var pos in doorList)
            {
                var doorPoint = pos switch
                {
                    "top" => ((int x, int y)?)(centerX, startY),
                    "bottom" => ((int x, int y)?)(centerX, endY),
                    "left" => ((int x, int y)?)(startX, centerY),
                    "right" => ((int x, int y)?)(endX, centerY),
                    "center_top" => ((int x, int y)?)(centerX, startY),
                    "center_bottom" => ((int x, int y)?)(centerX, endY),
                    "center_left" => ((int x, int y)?)(startX, centerY),
                    "center_right" => ((int x, int y)?)(endX, centerY),
                    _ => null
                };
                if (doorPoint != null) doorPositions.Add(doorPoint.Value);
            }

            var floorInfo = string.IsNullOrEmpty(floorDef) ? "" : $" | 已铺设 {width}x{height}={width * height} 格 {floorDef} 地板";
            var doorInfo = doorPositions.Count > 0 ? $" | {doorPositions.Count} 扇 {doorDef} 门" : "";

            return Task.FromResult(ToolResult.Success(
                $"已规划 {width}x{height} 房间 (中心 ({centerX},{centerY},{centerZ})):\n" +
                $"- 外墙: {wallCount} 格 {wallDef} 墙体 (范围 {startX}~{endX}, {startY}~{endY})\n" +
                $"- 内部空间: {width}x{height} = {width * height} 格" + doorInfo + floorInfo));
        }
    }
}
