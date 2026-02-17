using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed class BackgroundExecutionWorker
{
    private readonly BackgroundExecutionQueue _queue;
    private readonly INodeExecutionService _executor;

    public BackgroundExecutionWorker(BackgroundExecutionQueue queue, INodeExecutionService executor)
    {
        _queue = queue;
        _executor = executor;
    }

    public async Task RunAsync(CancellationToken token)
    {
        await foreach (var job in _queue.DequeueAllAsync(token))
        {
            await _executor.ExecuteAsync(job.Nodes, job.Connections, job.RuntimeStorage, job.NodeContext, job.Options, token)
                .ConfigureAwait(false);
        }
    }
}
