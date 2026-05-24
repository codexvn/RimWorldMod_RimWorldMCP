using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Verse;
using RimWorld;
using RimWorldMCP;

namespace RimWorldMCP.Tools
{
    public class Tool_ListBuildingMaterials : ITool
    {
        public string Name => "list_building_materials";
        public string Description => "列出当前游戏中所有可用作建筑材料的物品 DefName 和中文名称，供 designate_build / designate_room 的 stuff_defName 参数使用。动态读取 DefDatabase，Mod 添加的材料也会包含。";
        public JsonElement InputSchema => JsonSerializer.SerializeToElement(new
        {
            type = "object",
            properties = new
            {
                category = new
                {
                    type = "string",
                    description = "按类别过滤（可选）",
                    @enum = new[] { "all", "metallic", "stony", "woody", "other" },
                    @default = "all"
                }
            }
        });

        // 金属类 defName 模式匹配（忽略大小写）
        private static readonly HashSet<string> MetallicPatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            "Steel", "Plasteel", "Silver", "Gold", "Uranium", "Jade",
            "CompactedPlasteel", "CompactedSteel", "CompactedSilver",
            "CompactedGold", "CompactedUranium", "CompactedJade"
        };

        // 石砖类 defName 模式匹配
        private static readonly HashSet<string> StonyPatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            "GraniteBlocks", "MarbleBlocks", "LimestoneBlocks",
            "SandstoneBlocks", "SlateBlocks", "BlocksGranite",
            "BlocksMarble", "BlocksLimestone", "BlocksSandstone", "BlocksSlate",
            "Sandstone", "Granite", "Marble", "Limestone", "Slate"
        };

        // 木材类 defName 模式匹配
        private static readonly HashSet<string> WoodyPatterns = new(StringComparer.OrdinalIgnoreCase)
        {
            "WoodLog", "WoodPlank", "Lumber", "Bamboo"
        };

        public async Task<ToolResult> ExecuteAsync(JsonElement? args)
        {
            string category = "all";
            if (args != null && args.Value.TryGetProperty("category", out var jCat))
                category = jCat.GetString() ?? "all";

            return await McpCommandQueue.DispatchAsync(() =>
            {
                try
                {
                    var allStuff = DefDatabase<ThingDef>.AllDefs
                        .Where(d => d.IsStuff && !d.defName.StartsWith("_"))
                        .OrderBy(d => d.label)
                        .ThenBy(d => d.defName)
                        .ToList();

                    if (allStuff.Count == 0)
                        return ToolResult.Success("当前游戏没有可用建筑材料。");

                    // 分类 + 排序（按 category 过滤）
                    var stuffList = new List<(ThingDef def, string cat)>();
                    foreach (var def in allStuff)
                    {
                        string cat = ClassifyStuff(def.defName);
                        if (category == "all" || category == cat)
                            stuffList.Add((def, cat));
                    }

                    var catLabel = category switch
                    {
                        "metallic" => "金属类",
                        "stony" => "石砖类",
                        "woody" => "木材类",
                        "other" => "其他类",
                        _ => "全部"
                    };

                    var sb = new StringBuilder();
                    sb.AppendLine($"## 可用建筑材料 ({catLabel}, {stuffList.Count} 种)");
                    sb.AppendLine("| stuff_defName | 名称 | 类别 |");
                    sb.AppendLine("|---|---|");

                    string currentCat = "";
                    foreach (var (def, cat) in stuffList.OrderBy(s => s.cat).ThenBy(s => s.def.label))
                    {
                        if (cat != currentCat)
                        {
                            currentCat = cat;
                            string catHeader = currentCat switch
                            {
                                "metallic" => "**金属**",
                                "stony" => "**石砖**",
                                "woody" => "**木材**",
                                _ => "**其他**"
                            };
                            sb.AppendLine($"| | {catHeader} | |");
                        }
                        sb.AppendLine($"| `{def.defName}` | {def.label} | {cat} |");
                    }

                    return ToolResult.Success(sb.ToString().TrimEnd());
                }
                catch (Exception ex)
                {
                    return ToolResult.Error($"查询建筑材料失败: {ex.Message}");
                }
            });
        }

        private static string ClassifyStuff(string defName)
        {
            if (MetallicPatterns.Contains(defName))
                return "metallic";
            if (StonyPatterns.Contains(defName))
                return "stony";
            if (WoodyPatterns.Contains(defName))
                return "woody";
            // 模糊匹配：包含关键词
            if (defName.IndexOf("Steel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Plasteel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Silver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Gold", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Uranium", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Metal", StringComparison.OrdinalIgnoreCase) >= 0)
                return "metallic";
            if (defName.IndexOf("Block", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Brick", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Granite", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Marble", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Limestone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Sandstone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Slate", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Stone", StringComparison.OrdinalIgnoreCase) >= 0)
                return "stony";
            if (defName.IndexOf("Wood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Log", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Lumber", StringComparison.OrdinalIgnoreCase) >= 0 ||
                defName.IndexOf("Bamboo", StringComparison.OrdinalIgnoreCase) >= 0)
                return "woody";
            return "other";
        }
    }
}
