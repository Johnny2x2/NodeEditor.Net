namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop").Category("Conditions")
            .Description("Iterates a fixed number of times.")
            .Callable()
            .Input<int>("LoopTimes", 10)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var loopTimes = context.GetInput<int>("LoopTimes");
        for (int i = 0; i < loopTimes && !ct.IsCancellationRequested; i++)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }
        await context.TriggerAsync("Exit");
    }
}
