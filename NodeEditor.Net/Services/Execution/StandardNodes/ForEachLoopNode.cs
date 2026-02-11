using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForEachLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("ForEach Loop").Category("Conditions")
            .Description("Iterates over each item in a list.")
            .Callable()
            .Input<SerializableList>("List")
            .Output<object>("Obj")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var list = context.GetInput<SerializableList>("List");
        if (list is not null)
        {
            foreach (var item in list.Snapshot())
            {
                ct.ThrowIfCancellationRequested();
                context.SetOutput("Obj", item);
                await context.TriggerAsync("LoopPath");
            }
        }
        await context.TriggerAsync("Exit");
    }
}
