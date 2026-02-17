using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Iterates over each item in a list with bounded parallelism.
/// Each iteration executes in an isolated storage scope, so concurrent iterations
/// do not overwrite each other's outputs. Use <see cref="ForEachLoopNode"/> for
/// sequential iteration when ordering or shared state matters.
/// </summary>
public sealed class ParallelForEachNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Parallel ForEach").Category("Conditions")
            .Description("Iterates over a list in parallel with bounded concurrency. Each iteration runs in an isolated scope.")
            .Callable()
            .Input<SerializableList>("List")
            .Input<int>("MaxParallelism", 4)
            .Output<object>("Item")
            .Output<int>("Index")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var list = context.GetInput<SerializableList>("List");
        if (list is null || list.Count == 0)
        {
            await context.TriggerAsync("Exit");
            return;
        }

        var maxParallelism = context.GetInput<int>("MaxParallelism");
        if (maxParallelism < 1) maxParallelism = 1;

        var items = list.Snapshot();
        var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
        var tasks = new Task[items.Length];

        for (var i = 0; i < items.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            await semaphore.WaitAsync(ct).ConfigureAwait(false);

            var item = items[i];
            var index = i;

            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    // Create an isolated storage scope for this iteration.
                    // Read-through to parent gives access to upstream data;
                    // writes (outputs, execution marks) stay local.
                    var iterationScope = new LayeredRuntimeStorage(context.RuntimeStorage);

                    // Set the per-iteration outputs in the isolated scope
                    iterationScope.SetSocketValue(context.Node.Id, "Item", item);
                    iterationScope.SetSocketValue(context.Node.Id, "Index", index);

                    await context.TriggerScopedAsync("LoopPath", iterationScope).ConfigureAwait(false);
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct);
        }

        // Wait for all iterations to complete (or propagate first failure)
        await Task.WhenAll(tasks).ConfigureAwait(false);

        await context.TriggerAsync("Exit");
    }
}
