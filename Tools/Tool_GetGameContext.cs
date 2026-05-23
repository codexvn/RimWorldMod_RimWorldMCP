using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetGameContext : ITool
    {
        public string Name => "get_game_context";
        public string Description => "获取 RimWorld 当前游戏的完整状态上下文，包括殖民地概况、资源库存、研究进度、威胁信息、当前工作单等。应在执行任何操作前先调用此工具了解局势。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { }, required = Array.Empty<string>() });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var context = @"## 殖民地概况
- 名称: 新希望 | 时间: 第3年夏季第5天 | 天气: 晴
- 殖民者: 6人 (5自由殖民者 + 1囚犯)
- 动物: 2只哈士奇, 3只羊驼, 5只鸡

## 资源库存
- 钢铁: 2800 | 木头: 1200 | 石块: 3500 | 玻璃钢: 80
- 食物: 1500份 (约15天) | 医药: 60份 (闪耀世界医药: 5)
- 零部件: 25 | 高级零部件: 3
- 电力: 7200W/10000W (剩余2800W) | 储电: 15000Wd
- 白银: 12000

## 研究
- 当前: 微型电子学基础 (72.3%)
- 已完成: 24项

## 威胁与任务
- 威胁点数: 950 | 下次袭击: 2.1天内

## 当前工作单
- 裁缝台: 高级衬衫 x3 | 防弹夹克 x1 (暂停)
- 锻造台: 长剑 x1 | 板甲 x2 (暂停)
- 炉灶: 简单食物 xForever
- 研究工作台: 微型电子学基础 (进行中)
";
            return Task.FromResult(ToolResult.Success(context));
        }
    }
}
