using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RimWorldMCP.Mcp;
using RimWorldMCP.Skills;
using RimWorldMCP.Tools;
using RimWorldMCP.Transport;
using Verse;

namespace RimWorldMCP
{
    public class GameComponent_McpServer : GameComponent
    {
        private ITransport? _transport;
        private CancellationTokenSource? _cts;
        private const int DefaultPort = 9876;

        public GameComponent_McpServer(Game game)
        {
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            StartMcpService();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            StartMcpService();
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();
            McpCommandQueue.ProcessPending();
        }

        public override void ExposeData()
        {
            base.ExposeData();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }

        private void StartMcpService()
        {
            if (_transport != null) return; // 已启动

            try
            {
                // 1. 加载 SkillRegistry
                var skillsDir = FindSkillsDirectory();
                var skillRegistry = new SkillRegistry();
                skillRegistry.LoadFromDirectory(skillsDir);

                // 2. 创建 ToolRegistry + 注册 21 个 Tool
                var toolRegistry = new ToolRegistry();
                RegisterAllTools(toolRegistry, skillRegistry);

                // 3. 注册 Skill 资源
                foreach (var skill in skillRegistry.GetAll())
                {
                    toolRegistry.RegisterResource(
                        $"skill://{skill.Name}", skill.Name, skill.Description);
                }

                // 4. 创建 Transport（默认 SSE + Streamable HTTP，端口 9876）
                // 目前启动 Streamable HTTP（新版 MCP 规范推荐），SSE 可后续并行启动
                _transport = new StreamableHttpTransport(DefaultPort);

                // 5. 创建 McpServer
                var server = new McpServer(_transport, toolRegistry);

                // 6. 启动 Transport
                _cts = new CancellationTokenSource();
                _transport.StartAsync(_cts.Token);

                Log.Message($"[RimWorldMCP] MCP 服务已启动，端口: {DefaultPort}, 传输: http");
            }
            catch (Exception ex)
            {
                Log.Error($"[RimWorldMCP] 启动失败: {ex.Message}");
            }
        }

        private static void RegisterAllTools(ToolRegistry registry, SkillRegistry skillRegistry)
        {
            registry.Register(new Tool_GetGameContext());
            registry.Register(new Tool_GetResources());
            registry.Register(new Tool_ListRecipes());
            registry.Register(new Tool_CreateBill());
            registry.Register(new Tool_GetBills());
            registry.Register(new Tool_ManageBill());
            registry.Register(new Tool_DesignateBuild());
            registry.Register(new Tool_DesignateRoom());
            registry.Register(new Tool_ListResearchProjects());
            registry.Register(new Tool_GetResearchProgress());
            registry.Register(new Tool_SetResearchProject());
            registry.Register(new Tool_GetColonists());
            registry.Register(new Tool_GetColonistNeeds());
            registry.Register(new Tool_SetWorkPriority());
            registry.Register(new Tool_GetColonistHealth());
            registry.Register(new Tool_ScheduleOperation());
            registry.Register(new Tool_EquipPawn());
            registry.Register(new Tool_DraftPawn());
            registry.Register(new Tool_GetDefenseStatus());
            registry.Register(new Tool_GetSkills(skillRegistry));
            registry.Register(new Tool_ActiveSkill(skillRegistry));
        }

        private static string FindSkillsDirectory()
        {
            // mod 目录: publish/1.6/Assemblies/ → 向上一级找 Skills/
            var asmDir = Path.GetDirectoryName(typeof(GameComponent_McpServer).Assembly.Location)
                ?.Replace('\\', '/');
            if (asmDir == null) return "Skills";

            // Assemblies/ → 1.6/ → RimWorldMCP/
            var modDir = Path.GetFullPath(Path.Combine(asmDir, ".."));
            var skillsDir = Path.GetFullPath(Path.Combine(modDir, "..", "Skills"));

            if (Directory.Exists(skillsDir))
                return skillsDir;

            // 备选：相对路径
            var fallback = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skills"));
            return Directory.Exists(fallback) ? fallback : "Skills";
        }
    }
}
