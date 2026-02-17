namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DelayNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Delay").Category("Helpers")
            .Description("Pauses execution for a specified time.")
            .Callable()
            .Input<int>("DelayMs", 1000);
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var delayMs = context.GetInput<int>("DelayMs");
        await Task.Delay(delayMs, ct).ConfigureAwait(false);
        await context.TriggerAsync("Exit");
    }
}
