using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// A NodeBase subclass for testing streaming. Emits a configurable number
/// of string items via context.EmitAsync(), with optional delay between items.
/// </summary>
public sealed class TestStreamingNode : NodeBase
{
    public TestStreamingNode() { }

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Test Stream").Category("Test").Callable()
            .Input<int>("ItemCount", 3)
            .Input<int>("DelayPerItemMs", 0)
            .StreamOutput<string>("Item", "OnItem", "Completed");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var itemCount = context.GetInput<int>("ItemCount");
        var delayPerItemMs = context.GetInput<int>("DelayPerItemMs");

        for (int i = 0; i < itemCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (delayPerItemMs > 0) await Task.Delay(delayPerItemMs, ct);
            await context.EmitAsync("Item", $"item-{i}");
        }
        await context.TriggerAsync("Completed");
    }
}
