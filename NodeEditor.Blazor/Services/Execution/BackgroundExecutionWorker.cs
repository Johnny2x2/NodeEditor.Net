using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class BackgroundExecutionWorker
{
    private readonly BackgroundExecutionQueue _queue;
    private readonly NodeExecutionService _executor;

    public BackgroundExecutionWorker(BackgroundExecutionQueue queue, NodeExecutionService executor)
    {
        _queue = queue;
        _executor = executor;
    }

    public async Task RunAsync(CancellationToken token)
    {
        await foreach (var job in _queue.DequeueAllAsync(token))
        {
            await _executor.ExecutePlannedAsync(job.Plan, job.Connections, job.Context, job.NodeContext, job.Options, token).ConfigureAwait(false);
        }
    }
}
