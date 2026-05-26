using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RimWorldMCP.Skills;

namespace RimWorldMCP.Tools
{
    public class Tool_ActiveSkill : ITool
    {
        private readonly SkillRegistry _skillRegistry;

        public string Name => "active_skill";
        public string Description => "激活指定的领域知识技能（Skill），返回该 Skill 的完整内容。应在相关操作前激活。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new { skill_name = new { type = "string", description = "要激活的 Skill 名称" } },
            required = new[] { "skill_name" }
        });

        public Tool_ActiveSkill(SkillRegistry skillRegistry)
        {
            _skillRegistry = skillRegistry;
        }

        public Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            if (args == null || !args.Value.TryGetProperty("skill_name", out var nameProp))
            {
                var names = string.Join(", ", _skillRegistry.GetAll().Select(s => s.Name));
                return Task.FromResult(ToolResult.Error($"缺少参数: skill_name。可用 Skill: {names}"));
            }

            var name = nameProp.GetString() ?? "";
            var skill = _skillRegistry.Get(name);
            if (skill == null)
            {
                var names = string.Join(", ", _skillRegistry.GetAll().Select(s => s.Name));
                return Task.FromResult(ToolResult.Error($"Skill 不存在: {name}。可用 Skill: {names}"));
            }

            return Task.FromResult(ToolResult.Success($"## {skill.Name}\n\n{skill.Content}\n\n---\n> 来源: {Path.GetFileName(skill.FilePath)}"));
        }
        public (int minX, int minZ, int maxX, int maxZ)? GetTargetRange(JsonElement? args) => null;
    }
}
