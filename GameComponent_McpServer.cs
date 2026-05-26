using System;
using System.IO;
using System.Reflection;
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
        private static ITransport? s_activeTransport;
        internal static SkillRegistry? s_skillRegistry;
        private string _sessionId = "";
        private const int DefaultPort = 9877;
        private const string DefaultHost = "0.0.0.0";

        public GameComponent_McpServer(Game game)
        {
        }

        public override void StartedNewGame()
        {
            base.StartedNewGame();
            _sessionId = GenerateSessionId();
            DeteriorationTracker.Reset();
            StartMcpService();
            AttachMapUI();
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            if (string.IsNullOrEmpty(_sessionId))
                _sessionId = GenerateSessionId();
            DeteriorationTracker.Reset();
            StartMcpService();
            AttachMapUI();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref _sessionId, "mcpSessionId", "");
            TodoManager.ExposeData();
            TokenUsageTracker.ExposeData();
        }

        private static string GenerateSessionId()
        {
            return Guid.NewGuid().ToString("N").Substring(0, 12);
        }

        public override void GameComponentUpdate()
        {
            base.GameComponentUpdate();

            // 进入游戏自动打开对话窗口
            if (AutoOpenChat)
            {
                AutoOpenChat = false;
                try
                {
                    if (Find.CurrentMap != null && !Find.WindowStack.IsOpen<Dialog_AiChat>())
                        Find.WindowStack.Add(new Dialog_AiChat());
                }
                catch { /* 窗口创建失败不影响游戏 */ }
            }

            McpLog.Flush();
            McpCommandQueue.ProcessPending();
            Tool_AdvanceTick.ProcessPending();
            BridgeLifecycle.Tick();
            McpOssUploader.ProcessPendingUploads();
            McpCommandQueue.ProcessDeferredCleanup();
        }

        public override void FinalizeInit()
        {
            base.FinalizeInit();
        }

        private void StartMcpService()
        {
            // 先清理上一 Game 实例可能遗留的监听器（RimWorld 返回主菜单时 Game.Dispose 不通知 GameComponent）
            StopMcpService();

            try
            {
                // 1. 加载 SkillRegistry
                var skillsDir = FindSkillsDirectory();
                var skillRegistry = new SkillRegistry();
                skillRegistry.LoadFromDirectory(skillsDir);
                s_skillRegistry = skillRegistry;

                // 1.5 从 ModSettings 加载 OSS 配置
                if (RimWorldMCPMod.Instance != null)
                    McpOssConfig.LoadFromModSettings(RimWorldMCPMod.Instance.Settings);

                // 2. 创建 ToolRegistry + 注册 24 个 Tool
                var toolRegistry = new ToolRegistry();
                RegisterAllTools(toolRegistry, skillRegistry);

                // 3. 注册 Skill 资源
                foreach (var skill in skillRegistry.GetAll())
                {
                    toolRegistry.RegisterResource(
                        $"skill://{skill.Name}", skill.Name, skill.Description);
                }

                // 4. 创建 Transport（SSE + Streamable HTTP）
                var host = RimWorldMCPMod.Instance?.Settings?.McpHost ?? DefaultHost;
                var port = RimWorldMCPMod.Instance?.Settings?.McpPort ?? DefaultPort;
                var transport = new SseTransport(port, host);

                // 5. 创建 McpServer + 注入 /mcp 同步处理器
                var server = new McpServer(transport, toolRegistry);
                ((SseTransport)transport).SetMcpHandler(rawJson =>
                    server.ProcessMessageSync(rawJson));

                // 6. 启动 Transport（成功后才赋值 _transport，确保失败时下次可重试）
                _cts = new CancellationTokenSource();
                transport.StartAsync(_cts.Token);

                _transport = transport;
                s_activeTransport = transport;

                McpLog.Info($"MCP 服务已启动: http://{host}:{port}, 传输: http");

                McpLog.Info($"[session] ID = {_sessionId}");

                // 启动桥接器
                _ = BridgeLifecycle.StartAsync(_sessionId);
            }
            catch (Exception ex)
            {
                // 启动失败时清理 _cts，_transport 保持 null 以便下次重试
                if (_cts != null)
                {
                    try { _cts.Cancel(); _cts.Dispose(); } catch { }
                    _cts = null;
                }
                McpLog.Error($"启动失败: {ex.Message}");
            }
        }

        private void StopMcpService()
        {
            if (_transport != null)
            {
                try { _transport.StopAsync(); } catch (Exception ex) { McpLog.Warn($"停止传输时出错: {ex.Message}"); }
                _transport = null;
            }

            if (s_activeTransport != null && s_activeTransport != _transport)
            {
                try { s_activeTransport.StopAsync(); } catch (Exception ex) { McpLog.Warn($"停止残留传输时出错: {ex.Message}"); }
                s_activeTransport = null;
            }

            if (_cts != null)
            {
                try { _cts.Cancel(); _cts.Dispose(); } catch { }
                _cts = null;
            }

            TodoManager.Clear();
            BridgeLifecycle.Stop();
        }

        private static void RegisterAllTools(ToolRegistry registry, SkillRegistry skillRegistry)
        {
            foreach (var type in typeof(ToolRegistry).Assembly.GetTypes())
            {
                if (!typeof(ITool).IsAssignableFrom(type) || type.IsInterface || type.IsAbstract)
                    continue;

                McpLog.Info($"动态注册工具: {type.Name}");
                try
                {
                    ITool? tool;
                    var ctorWithSkill = type.GetConstructor(new[] { typeof(SkillRegistry) });
                    if (ctorWithSkill != null)
                        tool = (ITool)ctorWithSkill.Invoke(new object[] { skillRegistry });
                    else
                        tool = (ITool)Activator.CreateInstance(type);

                    if (tool != null)
                        registry.Register(tool);
                }
                catch (Exception ex)
                {
                    McpLog.Warn($"注册失败 {type.Name}: {ex.Message}");
                }
            }
        }

        private static string FindSkillsDirectory()
        {
            try
            {
                // 首选：通过 Mod.Content.RootDir 定位（最可靠，不依赖 Assembly.Location）
                var modRoot = TryGetModRootDir();
                if (modRoot != null)
                {
                    var skillsDir = Path.Combine(modRoot, "Skills");
                    McpLog.Info($"[skills] Mod.Content.RootDir Skills 路径 = {skillsDir} (Exists={Directory.Exists(skillsDir)})");
                    if (Directory.Exists(skillsDir))
                        return skillsDir;
                }

                // 备选：Assembly.Location 向上两级（仅当 Assembly 从磁盘加载时可用）
                var asmPath = typeof(GameComponent_McpServer).Assembly.Location;
                if (!string.IsNullOrEmpty(asmPath))
                {
                    var asmDir = Path.GetDirectoryName(asmPath);
                    if (asmDir != null)
                    {
                        var asmModRoot = Path.GetFullPath(Path.Combine(asmDir, "..", ".."));
                        var skillsDir = Path.Combine(asmModRoot, "Skills");
                        McpLog.Info($"[skills] Assembly.Location Skills 路径 = {skillsDir} (Exists={Directory.Exists(skillsDir)})");
                        if (Directory.Exists(skillsDir))
                            return skillsDir;
                    }
                }
            }
            catch (Exception ex)
            {
                McpLog.Warn($"[skills] 查找 Skills 目录异常: {ex.Message}");
            }

            // 最终后备
            var fallback = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Skills");
            McpLog.Info($"[skills] 后备 Skills 路径 = {fallback} (Exists={Directory.Exists(fallback)})");
            if (Directory.Exists(fallback)) return fallback;
            McpLog.Warn("[skills] 所有路径均未找到 Skills 目录，返回相对路径 'Skills'");
            return "Skills";
        }

        private static string? TryGetModRootDir()
        {
            try
            {
                var content = RimWorldMCPMod.Instance?.Content;
                if (content != null && !string.IsNullOrEmpty(content.RootDir))
                    return content.RootDir;
            }
            catch { }
            return null;
        }

        /// <summary>新游戏/加载后自动打开对话窗口</summary>
        internal static bool AutoOpenChat;

        private static void AttachMapUI()
        {
            var map = Find.CurrentMap;
            if (map == null) return;
            // 防止重复添加
            foreach (var c in map.components)
                if (c is MapComponent_McpUI) return;
            map.components.Add(new MapComponent_McpUI(map));
            AutoOpenChat = true;
        }

    }
}
