namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForLoopStepNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop Step").Category("Conditions")
            .Description("Iterates from start to end with a configurable step.")
            .Callable()
            .Input<int>("StartValue", 0).Input<int>("EndValue", 10).Input<int>("Step", 1)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var start = context.GetInput<int>("StartValue");
        var end = context.GetInput<int>("EndValue");
        var step = context.GetInput<int>("Step");
        if (step == 0) step = 1;
        for (int i = start; (step > 0 ? i < end : i > end) && !ct.IsCancellationRequested; i += step)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }
        await context.TriggerAsync("Exit");
    }
}
