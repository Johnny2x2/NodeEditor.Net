namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugErrorNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Debug Error").Category("Debug")
            .Description("Emits an error message.")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        context.EmitFeedback(message, ExecutionFeedbackType.Break);
        await context.TriggerAsync("Exit");
    }
}
