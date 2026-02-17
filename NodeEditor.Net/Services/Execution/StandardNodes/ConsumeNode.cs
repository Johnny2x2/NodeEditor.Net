namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ConsumeNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Consume").Category("Helpers")
            .Description("Consumes a value (forces upstream evaluation) and continues.")
            .Callable()
            .Input<object>("Value");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        _ = context.GetInput<object>("Value");
        await context.TriggerAsync("Exit");
    }
}
