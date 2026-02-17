namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class MarkerNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Marker").Category("Helpers")
            .Description("Passthrough marker for organizing flows.")
            .Callable();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
