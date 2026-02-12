using NodeEditor.Net.Services.Execution;
using System.Collections.Concurrent;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// A NodeBase subclass for testing. Captures all received items
/// into a <see cref="Collected"/> list for assertion.
/// </summary>
public sealed class TestCollectorNode : NodeBase
{
    public TestCollectorNode() { }

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Collector").Category("Test").Callable()
            .Input<string>("Key", "collector")
            .Input<object>("Value");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var key = context.GetInput<string>("Key");
        var value = context.GetInput<object>("Value");

        if (context.GetVariable(key) is not ConcurrentQueue<object?> queue)
        {
            queue = new ConcurrentQueue<object?>();
            context.SetVariable(key, queue);
        }

        queue.Enqueue(value);
        return context.TriggerAsync("Exit");
    }
}
