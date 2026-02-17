namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class StartNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Start")
            .Category("Helpers")
            .Description("Entry point for execution. Place one to begin a flow.")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
