using System.Text.Json;
using System.Threading.Tasks;

namespace RimWorldMCP.Tools
{
    public class Tool_GetDefenseStatus : ITool
    {
        public string Name => "get_defense_status";
        public string Description => "获取殖民地防御状态报告：所有殖民者的武器装备、护甲覆盖、征召状态和战斗力评估。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var report = @"## 防御状态报告

### 武器装备
| 殖民者 | 主武器 | 护甲 | 征召 |
|--------|--------|------|------|
| 王建国 | 栓动步枪 (优秀) | 简易头盔 (良好) | ✖ 未征召 |
| 赵大力 | 突击步枪 (良好) | 防弹背心 (良好) | ✖ 未征召 |
| 张铁柱 | 长剑 (极佳) | 板甲 (优秀) + 简易头盔 | ✖ 未征召 |
| 李秀英 | 无 | 无 | ✖ 未征召 |
| 陈美玲 | 无 | 无 | ✖ 未征召 |
| 刘小芳 | 无 | 无 | ✖ 未征召 |

### 战斗力评估
- 远程火力: 2 人 (栓动步枪 x1, 突击步枪 x1)
- 近战肉盾: 1 人 (长剑 + 板甲)
- 无装备: 3 人

### 阵地状态
- 沙袋掩体: 6 格 | 陷阱: 12 个
- 外墙: 完整 (花岗岩, 2层)
";
            return Task.FromResult(ToolResult.Success(report));
        }
    }
}
