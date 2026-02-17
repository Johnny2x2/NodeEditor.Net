namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugPrintNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Debug Print").Category("Debug")
            .Description("Prints a labeled value to the debug output.")
            .Callable()
            .Input<string>("Label", "").Input<object>("Value");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var label = context.GetInput<string>("Label");
        var value = context.GetInput<object>("Value");
        var message = string.IsNullOrEmpty(label) ? $"{value}" : $"{label}: {value}";
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        await context.TriggerAsync("Exit");
    }
}
