using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetResources : ITool
    {
        public string Name => "get_resources";
        public string Description => "获取殖民地当前资源库存详细报告，包括基础材料、食物、药品、装备、电力等。用于评估制造能力和资源瓶颈。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var report = @"## 资源库存报告

### 基础材料
- 钢铁: 2800 | 木头: 1200 | 石块: 3500 (花岗岩) | 玻璃钢: 80
- 金: 150 | 铀: 40 | 翡翠: 25

### 加工材料
- 零部件: 25 | 高级零部件: 3
- 布匹: 320 | 恶魔线: 45

### 食物
- 简单食物: 45份 | 精致食物: 18份 | 奢侈食物: 5份
- 生肉: 200份 | 稻米: 350份 | 玉米: 280份
- 啤酒: 60瓶

### 医药
- 草药: 30份 | 普通医药: 60份 | 闪耀世界医药: 5份

### 装备库存
- 栓动步枪: 3 | 突击步枪: 1 | 冲锋手枪: 2
- 简易头盔: 8 | 防弹背心: 2 | 板甲: 1

### 电力
- 发电: 10000W | 用电: 7200W | 储电: 15000Wd / 20000Wd
";
            return Task.FromResult(ToolResult.Success(report));
        }
    }
}
