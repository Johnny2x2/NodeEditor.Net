namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class PrintValueNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Print Value").Category("Debug")
            .Description("Prints a value and passes it through.")
            .Input<string>("Label", "").Input<object>("Value")
            .Output<object>("PassThrough");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var label = context.GetInput<string>("Label");
        var value = context.GetInput<object>("Value");
        var message = string.IsNullOrEmpty(label) ? $"{value}" : $"{label}: {value}";
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        context.SetOutput("PassThrough", value);
        return Task.CompletedTask;
    }
}
