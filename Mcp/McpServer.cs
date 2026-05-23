using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RimWorldMCP.Tools;
using RimWorldMCP.Transport;

namespace RimWorldMCP.Mcp
{
    public class McpServer
    {
        private readonly ITransport _transport;
        private readonly ToolRegistry _toolRegistry;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _inflight = new();

        public McpServer(ITransport transport, ToolRegistry toolRegistry)
        {
            _transport = transport;
            _toolRegistry = toolRegistry;
            _transport.OnMessage += OnMessageReceived;
        }

        private void OnMessageReceived(string rawJson)
        {
            _ = HandleMessageAsync(rawJson);
        }

        // ---- 消息入口 ----

        private async Task HandleMessageAsync(string rawJson)
        {
            JsonRpcRequest? request;

            try
            {
                request = JsonSerializer.Deserialize<JsonRpcRequest>(rawJson, McpJson.Options);
            }
            catch
            {
                await SendError(null, -32700, "Parse error: 无效的 JSON");
                return;
            }

            if (request == null)
            {
                await SendError(null, -32700, "Parse error: 无法解析");
                return;
            }

            // JSON-RPC 规范：必须校验 jsonrpc == "2.0"
            if (request.Jsonrpc != "2.0")
            {
                await SendError(request.Id, -32600, "Invalid Request: jsonrpc 必须为 \"2.0\"");
                return;
            }

            try
            {
                var response = await DispatchAsync(request);
                if (response != null)
                {
                    await _transport.SendAsync(response.ToJson());
                }
            }
            catch (Exception ex)
            {
                Log($"Dispatch 异常: {ex}");
                await SendError(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }

        // ---- 方法路由 ----

        private async Task<JsonRpcResponse?> DispatchAsync(JsonRpcRequest request)
        {
            var isNotification = request.IsNotification;

            switch (request.Method)
            {
                case "initialize":
                    return HandleInitialize(request.Id, request.Params);

                case "notifications/initialized":
                    Log("MCP 初始化完成");
                    return null;

                case "notifications/cancelled":
                    HandleCancelled(request.Params);
                    return null;

                case "tools/list":
                    return HandleToolsList(request.Id);

                case "tools/call":
                    return await HandleToolsCallAsync(request);

                case "resources/list":
                    return HandleResourcesList(request.Id);

                case "resources/read":
                    return HandleResourcesRead(request.Id, request.Params);

                case "ping":
                    return HandlePing(request.Id);

                default:
                    // 通知（无 id）的未知方法 → 静默忽略
                    if (isNotification) return null;
                    return JsonRpcResponse.Fail(request.Id!.Value, -32601,
                        $"Method not found: {request.Method}");
            }
        }

        // ---- initialize: 协议版本协商 ----

        private JsonRpcResponse HandleInitialize(JsonElement? id, JsonElement? @params)
        {
            var clientVersion = "2024-11-05";

            if (@params != null)
            {
                try
                {
                    var init = JsonSerializer.Deserialize<InitializeParams>(
                        @params.Value.GetRawText(), McpJson.Options);
                    if (init != null && !string.IsNullOrEmpty(init.ProtocolVersion))
                        clientVersion = init.ProtocolVersion;
                }
                catch { /* 无法解析客户端参数，使用默认版本 */ }
            }

            // 我们只支持 2024-11-05，选择双方都支持的最高版本
            var negotiated = clientVersion == "2024-11-05" ? "2024-11-05" : "2024-11-05";

            var result = new InitializeResult { ProtocolVersion = negotiated };
            if (id != null)
            {
                return JsonRpcResponse.Success(id.Value, result);
            }
            return new JsonRpcResponse
            {
                Id = null,
                Result = JsonSerializer.SerializeToElement(result, McpJson.Options)
            };
        }

        // ---- tools/list ----

        private JsonRpcResponse HandleToolsList(JsonElement? id)
        {
            var tools = _toolRegistry.GetDefinitions();
            return JsonRpcResponse.Success(id!.Value, new { tools });
        }

        // ---- tools/call ----

        private async Task<JsonRpcResponse> HandleToolsCallAsync(JsonRpcRequest request)
        {
            var id = request.Id!.Value;

            if (request.Params == null)
                return JsonRpcResponse.Fail(id, -32602, "缺少 params");

            ToolCallParams? callParams;
            try
            {
                callParams = JsonSerializer.Deserialize<ToolCallParams>(
                    request.Params.Value.GetRawText(), McpJson.Options);
            }
            catch
            {
                return JsonRpcResponse.Fail(id, -32602, "无法解析 tool call 参数");
            }

            if (callParams == null || string.IsNullOrEmpty(callParams.Name))
                return JsonRpcResponse.Fail(id, -32602, "缺少 tool name");

            // 注册到 inflight 列表，支持取消
            var requestId = request.Id?.GetRawText() ?? callParams.Name;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            _inflight[requestId] = cts;

            try
            {
                var result = await _toolRegistry.ExecuteAsync(callParams.Name, callParams.Arguments);
                return JsonRpcResponse.Success(id, result);
            }
            catch (OperationCanceledException)
            {
                return JsonRpcResponse.Fail(id, -32800, "Request cancelled");
            }
            finally
            {
                _inflight.TryRemove(requestId, out _);
                cts.Dispose();
            }
        }

        // ---- notifications/cancelled ----

        private void HandleCancelled(JsonElement? @params)
        {
            if (@params == null) return;

            try
            {
                var json = @params.Value.GetRawText();
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("requestId", out var reqId))
                {
                    var key = reqId.GetRawText();
                    if (_inflight.TryGetValue(key, out var cts))
                    {
                        cts.Cancel();
                        Log($"请求已取消: {key}");
                    }
                }
            }
            catch { /* 静默忽略无效的取消通知 */ }
        }

        // ---- resources/list ----

        private JsonRpcResponse HandleResourcesList(JsonElement? id)
        {
            var resources = _toolRegistry.GetResources();
            return JsonRpcResponse.Success(id!.Value, new { resources });
        }

        // ---- resources/read ----

        private JsonRpcResponse HandleResourcesRead(JsonElement? id, JsonElement? @params)
        {
            if (@params == null || !@params.Value.TryGetProperty("uri", out var uri))
                return JsonRpcResponse.Fail(id!.Value, -32602, "缺少 uri 参数");

            var content = _toolRegistry.ReadResource(uri.GetString() ?? "");
            if (content == null)
                return JsonRpcResponse.Fail(id!.Value, -32000, $"Resource not found: {uri}");

            return JsonRpcResponse.Success(id!.Value, new
            {
                contents = new[]
                {
                    new { uri = uri.GetString(), mimeType = "text/markdown", text = content }
                }
            });
        }

        // ---- ping ----

        private static JsonRpcResponse HandlePing(JsonElement? id)
        {
            return JsonRpcResponse.Success(id!.Value, new { });
        }

        // ---- 错误发送 ----

        private async Task SendError(JsonElement? id, int code, string message)
        {
            // 通知不返回响应
            if (id == null || id.Value.ValueKind == JsonValueKind.Null)
                return;

            var resp = JsonRpcResponse.Fail(id.Value, code, message);
            await _transport.SendAsync(resp.ToJson());
        }

        private static void Log(string msg) => McpLog.Info(msg);
    }
}
