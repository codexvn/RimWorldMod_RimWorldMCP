using System;
using System.Threading;
using System.Threading.Tasks;

namespace RimWorldMCP.Transport
{
    public interface ITransport
    {
        string Name { get; }
        Task StartAsync(CancellationToken ct);
        Task SendAsync(string message);
        event Action<string> OnMessage;
        Task StopAsync();
    }
}
