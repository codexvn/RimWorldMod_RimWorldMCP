using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace RimWorldMCP
{
    public class McpCommand
    {
        public Func<object?> Action { get; set; }
        public TaskCompletionSource<object?> Completion { get; set; } = new();
        public CancellationTokenSource Timeout { get; set; } = new(TimeSpan.FromSeconds(5));
    }

    public static class McpCommandQueue
    {
        private static readonly ConcurrentQueue<McpCommand> _queue = new();

        public static void Enqueue(McpCommand command)
        {
            _queue.Enqueue(command);
        }

        public static void ProcessPending()
        {
            while (_queue.TryDequeue(out var command))
            {
                try
                {
                    if (command.Timeout.IsCancellationRequested)
                    {
                        command.Completion.TrySetException(
                            new TimeoutException("命令执行超时（5秒内未被主线程处理）"));
                        continue;
                    }

                    var result = command.Action();
                    command.Completion.TrySetResult(result);
                }
                catch (Exception ex)
                {
                    command.Completion.TrySetException(ex);
                }
                finally
                {
                    command.Timeout.Dispose();
                }
            }
        }

        public static int PendingCount => _queue.Count;
    }
}
