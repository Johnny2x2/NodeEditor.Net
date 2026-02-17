namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugWarningNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Debug Warning").Category("Debug")
            .Description("Emits a warning message.")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        context.EmitFeedback(message, ExecutionFeedbackType.Continue);
        await context.TriggerAsync("Exit");
    }
}
