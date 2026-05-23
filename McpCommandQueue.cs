using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RimWorldMCP
{
    public class McpCommand
    {
        public Func<object?> Action { get; set; } = null!;
        public TaskCompletionSource<object?> Completion { get; set; } = new();
    }

    public static class McpCommandQueue
    {
        private static readonly ConcurrentQueue<McpCommand> _queue = new();

        public static void Enqueue(McpCommand command)
        {
            _queue.Enqueue(command);
        }

        /// <summary>主线程每帧调用。无条件执行所有待处理命令。</summary>
        public static void ProcessPending()
        {
            while (_queue.TryDequeue(out var command))
            {
                try
                {
                    var result = command.Action();
                    command.Completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    command.Completion.TrySetException(ex);
                }
            }
        }

        public static int PendingCount => _queue.Count;

        /// <summary>调度同步操作到主线程执行，等待结果（超时 30 秒）</summary>
        public static async Task<T> DispatchAsync<T>(Func<T> action, int timeoutMs = 30000)
        {
            var command = new McpCommand { Action = () => action() };
            _queue.Enqueue(command);

            var timeout = Task.Delay(timeoutMs);
            var completed = await Task.WhenAny(command.Completion.Task, timeout);
            if (completed == timeout)
                throw new TimeoutException("主线程命令执行超时");

            return (T)command.Completion.Task.Result!;
        }
    }
}
