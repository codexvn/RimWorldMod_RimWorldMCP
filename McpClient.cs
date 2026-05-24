using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace RimWorldMCP
{
    public enum ClientState { Disconnected, Connecting, Handshake, Ready }

    public static class McpClient
    {
        private static ClientWebSocket? _ws;
        private static CancellationTokenSource? _cts;
        private static string _url = "";
        private static string _token = "";
        private static int _rpcSeq;
        private static ClientState _state = ClientState.Disconnected;
        private static TaskCompletionSource<bool>? _helloOk;
        private static int _tickIntervalMs = 30000; // 默认 30s，hello-ok 可能覆盖
        private static DateTime _lastTick = DateTime.MinValue;

        public static ClientState State => _state;
        public static bool IsConnected => _state >= ClientState.Handshake;
        public static bool IsReady => _state == ClientState.Ready;

        public static readonly ConcurrentQueue<string> Incoming = new();

        /// <summary>连接 Gateway</summary>
        public static async Task Connect(string wsUrl, string token, string password)
        {
            _url = wsUrl;
            _token = !string.IsNullOrEmpty(token) ? token : password;
            Disconnect();

            try
            {
                _ws = new ClientWebSocket();
                _cts = new CancellationTokenSource();
                _helloOk = new TaskCompletionSource<bool>();
                _state = ClientState.Connecting;

                await _ws.ConnectAsync(new Uri(wsUrl), _cts.Token);
                _state = ClientState.Handshake;
                McpLog.Info($"[ws] 已连接: {wsUrl}");

                // 启动接收循环
                _ = ReceiveLoop(_cts.Token);

                // 等待 hello-ok (最多 15 秒)
                var timeout = Task.Delay(15000);
                var completed = await Task.WhenAny(_helloOk.Task, timeout);
                if (completed == _helloOk.Task && _helloOk.Task.Result)
                {
                    _state = ClientState.Ready;
                    McpLog.Info("[ws] 握手完成");
                }
                else
                {
                    McpLog.Warn("[ws] 握手超时，尝试直接进入 Ready");
                    // 简易模式：直接发 connect 轻量握手
                    await SendJson(new { type = "connect", role = "client", client = "csharp" });
                    if (!string.IsNullOrEmpty(_token))
                        await SendJson(new { type = "auth", token = _token });
                    _state = ClientState.Ready;
                }
            }
            catch (Exception ex)
            {
                _state = ClientState.Disconnected;
                McpLog.Warn($"[ws] 连接失败: {ex.Message}");
            }
        }

        public static async Task SendMessage(string text)
        {
            if (!IsReady) return;
            await SendRpc("agent.send", new { text });
        }

        public static async Task SendRpc(string method, object? payload = null)
        {
            if (!IsReady) return;
            var id = (++_rpcSeq).ToString();
            await SendJson(new { type = "req", id, method, @params = payload });
        }

        public static async Task Ping()
        {
            if (_ws?.State == WebSocketState.Open)
                await SendJson(new { type = "ping" });
        }

        public static void Disconnect()
        {
            _cts?.Cancel();
            _state = ClientState.Disconnected;
            try { _ws?.Dispose(); } catch { }
            _ws = null;
        }

        private static async Task SendJson(object obj)
        {
            if (_ws?.State != WebSocketState.Open) return;
            var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None);
        }

        // Gateway 完整握手：收到 challenge → 发 connect RPC → 等 hello-ok
        // backend + loopback + token 认证路径，可省略 device 签名（官方文档明确允许）
        private static async Task SendChallengeResponse(string nonce)
        {
            var connectParams = new
            {
                minProtocol = 3,
                maxProtocol = 4,
                client = new
                {
                    id = "gateway-client",
                    displayName = "RimWorldMCP",
                    version = "1.0",
                    platform = Environment.OSVersion.Platform.ToString().Contains("Win") ? "windows"
                        : Environment.OSVersion.Platform.ToString().Contains("Mac") ? "macos" : "linux",
                    mode = "backend"
                },
                role = "operator",
                scopes = new[] { "operator.read", "operator.write" },
                caps = new[] { "tool-events" },
                locale = "zh-CN",
                userAgent = "RimWorldMCP/1.0",
                auth = new
                {
                    token = _token,
                    password = (string?)null,
                    deviceToken = (string?)null
                },
                device = new
                {
                    id = "rimworld-mcp",
                    nonce
                }
            };

            await SendJson(new { type = "req", id = Guid.NewGuid().ToString("N").Substring(0, 8), method = "connect", @params = connectParams });
        }

        private static async Task ReceiveLoop(CancellationToken ct)
        {
            var buf = new byte[8192];
            try
            {
                while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
                {
                    var result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf), ct);
                    if (result.MessageType == WebSocketMessageType.Close) break;

                    var text = Encoding.UTF8.GetString(buf, 0, result.Count);
                    while (!result.EndOfMessage && !ct.IsCancellationRequested)
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buf, result.Count, buf.Length - result.Count), ct);
                        text += Encoding.UTF8.GetString(buf, 0, result.Count);
                    }

                    Incoming.Enqueue(text);

                    // 解析握手阶段的关键消息
                    try
                    {
                        using var doc = JsonDocument.Parse(text);
                        var root = doc.RootElement;
                        if (!root.TryGetProperty("type", out var t)) continue;
                        var type = t.GetString();

                        if (type == "evt" || type == "event")
                        {
                            if (root.TryGetProperty("event", out var ev) && ev.GetString() == "connect.challenge"
                                && root.TryGetProperty("payload", out var pl) && pl.TryGetProperty("nonce", out var nonce))
                            {
                                await SendChallengeResponse(nonce.GetString() ?? "");
                            }
                        }
                        else if (type == "res")
                        {
                            if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean()
                                && root.TryGetProperty("payload", out var payload)
                                && payload.TryGetProperty("type", out var pt) && pt.GetString() == "hello-ok")
                            {
                                // 从 hello-ok 读取 tick 间隔
                                if (payload.TryGetProperty("policy", out var policy)
                                    && policy.TryGetProperty("tickIntervalMs", out var tiv) && tiv.TryGetInt32(out var iv))
                                    _tickIntervalMs = iv;

                                _lastTick = DateTime.UtcNow;
                                _helloOk?.TrySetResult(true);
                            }
                        }
                        else if (type == "event" || type == "evt")
                        {
                            if (root.TryGetProperty("event", out var ev) && ev.GetString() == "tick")
                                _lastTick = DateTime.UtcNow;
                        }

                        // tick 超时检查 (2x tickIntervalMs)
                        if (_state == ClientState.Ready && (DateTime.UtcNow - _lastTick).TotalMilliseconds > _tickIntervalMs * 2)
                        {
                            McpLog.Warn("[ws] tick 超时，连接可能已断开");
                            _state = ClientState.Disconnected;
                            try { _ws?.CloseAsync(WebSocketCloseStatus.ProtocolError, "tick timeout", CancellationToken.None); } catch { }
                            break;
                        }
                    }
                    catch { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { McpLog.Warn($"[ws] 接收异常: {ex.Message}"); }

            _state = ClientState.Disconnected;
        }
    }
}
