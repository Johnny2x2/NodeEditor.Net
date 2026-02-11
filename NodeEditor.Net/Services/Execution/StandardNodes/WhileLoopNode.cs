namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class WhileLoopNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("While Loop").Category("Conditions")
            .Description("Loops while a condition is true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        while (context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested)
        {
            await context.TriggerAsync("LoopPath");
            iteration++;
        }
        await context.TriggerAsync("Exit");
    }
}
