using System.Threading.Channels;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed record ExecutionJob(
    Guid Id,
    ExecutionPlan Plan,
    IReadOnlyList<ConnectionData> Connections,
    INodeRuntimeStorage Context,
    object NodeContext,
    NodeExecutionOptions Options);

public sealed class BackgroundExecutionQueue
{
    private readonly Channel<ExecutionJob> _queue = Channel.CreateUnbounded<ExecutionJob>();

    public ValueTask EnqueueAsync(ExecutionJob job) => _queue.Writer.WriteAsync(job);

    public IAsyncEnumerable<ExecutionJob> DequeueAllAsync(CancellationToken token) => _queue.Reader.ReadAllAsync(token);
}
