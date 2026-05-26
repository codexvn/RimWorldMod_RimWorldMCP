using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RimWorldMCP.Skills;

namespace RimWorldMCP.Tools
{
    public class Tool_GetSkills : ITool
    {
        private readonly SkillRegistry _skillRegistry;

        public string Name => "get_skills";
        public string Description => "列出所有可用的领域知识技能（Skill）。每个 Skill 是一份领域指南，可用 active_skill 激活获取详细内容。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new { type = "object", properties = new { } });

        public Tool_GetSkills(SkillRegistry skillRegistry)
        {
            _skillRegistry = skillRegistry;
        }

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            var skills = _skillRegistry.GetAll();
            if (skills.Count == 0)
                return Task.FromResult(ToolResult.Success("当前没有可用的 Skill。"));

            var lines = skills.Select(s => $"- **{s.Name}**: {s.Description}");
            var result = $"可用 Skill ({skills.Count} 个):\n\n{string.Join("\n", lines)}\n\n使用 active_skill(skill_name) 来激活获取完整内容。";
            return Task.FromResult(ToolResult.Success(result));
        }
        public (int minX, int minZ, int maxX, int maxZ)? GetTargetRange(JsonElement? args) => null;
    }
}
