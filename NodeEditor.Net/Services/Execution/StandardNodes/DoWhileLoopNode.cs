namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DoWhileLoopNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Do While Loop").Category("Conditions")
            .Description("Executes body at least once, then loops while condition is true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        do {
            ct.ThrowIfCancellationRequested();
            await context.TriggerAsync("LoopPath");
            iteration++;
        } while (context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested);
        await context.TriggerAsync("Exit");
    }
}
