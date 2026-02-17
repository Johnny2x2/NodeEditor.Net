namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class BranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Branch")
            .Category("Conditions")
            .Description("Branch execution on a boolean condition.")
            .ExecutionInput("Start")
            .Input<bool>("Cond")
            .ExecutionOutput("True")
            .ExecutionOutput("False");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var cond = context.GetInput<bool>("Cond");
        await context.TriggerAsync(cond ? "True" : "False");
    }
}
