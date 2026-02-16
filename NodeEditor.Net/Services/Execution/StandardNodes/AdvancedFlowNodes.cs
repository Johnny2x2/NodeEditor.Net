using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// ForEach Dict — iterates over key-value pairs in a dictionary.
/// </summary>
public sealed class ForEachDictNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("ForEach Dict").Category("Conditions")
            .Description("Iterates over each key-value pair in a dictionary.")
            .Callable()
            .Input<SerializableDict>("Dict")
            .Output<string>("Key")
            .Output<object>("Value")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var dict = context.GetInput<SerializableDict>("Dict");
        if (dict is not null)
        {
            foreach (var kvp in dict.Snapshot())
            {
                ct.ThrowIfCancellationRequested();
                context.SetOutput("Key", kvp.Key);
                context.SetOutput("Value", kvp.Value);
                await context.TriggerAsync("LoopPath");
            }
        }
        await context.TriggerAsync("Exit");
    }
}

/// <summary>
/// For Loop Reverse — iterates from a start value down to an end value.
/// </summary>
public sealed class ForLoopReverseNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("For Loop Reverse").Category("Conditions")
            .Description("Iterates from Start down to End (inclusive).")
            .Callable()
            .Input<int>("Start", 9)
            .Input<int>("End", 0)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var start = context.GetInput<int>("Start");
        var end = context.GetInput<int>("End");
        for (int i = start; i >= end && !ct.IsCancellationRequested; i--)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }
        await context.TriggerAsync("Exit");
    }
}

/// <summary>
/// Loop N Times — simple repeat N times loop.
/// </summary>
public sealed class LoopNTimesNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Loop N Times").Category("Conditions")
            .Description("Repeats the body path exactly N times.")
            .Callable()
            .Input<int>("N", 5)
            .Output<int>("Iteration")
            .ExecutionOutput("Body")
            .ExecutionOutput("Done");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var n = Math.Max(0, context.GetInput<int>("N"));
        for (int i = 0; i < n && !ct.IsCancellationRequested; i++)
        {
            context.SetOutput("Iteration", i);
            await context.TriggerAsync("Body");
        }
        await context.TriggerAsync("Done");
    }
}

/// <summary>
/// Multi Branch — branches execution based on multiple boolean conditions, 
/// triggering the first match or Default.
/// </summary>
public sealed class MultiBranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Multi Branch").Category("Conditions")
            .Description("Evaluates conditions in order and triggers the first true path, or Default.")
            .Callable()
            .Input<bool>("Cond 0")
            .Input<bool>("Cond 1")
            .Input<bool>("Cond 2")
            .Input<bool>("Cond 3")
            .ExecutionOutput("Path 0")
            .ExecutionOutput("Path 1")
            .ExecutionOutput("Path 2")
            .ExecutionOutput("Path 3")
            .ExecutionOutput("Default");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        for (int i = 0; i < 4; i++)
        {
            if (context.GetInput<bool>($"Cond {i}"))
            {
                await context.TriggerAsync($"Path {i}");
                return;
            }
        }
        await context.TriggerAsync("Default");
    }
}

/// <summary>
/// Parallel Branch — triggers all connected outputs simultaneously (using TriggerAsync sequentially;
/// true parallel would require engine-level support).
/// </summary>
public sealed class ParallelBranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Parallel Branch").Category("Flow")
            .Description("Triggers all output paths in sequence (A, B, C, D). Useful for fan-out patterns.")
            .Callable()
            .ExecutionOutput("A")
            .ExecutionOutput("B")
            .ExecutionOutput("C")
            .ExecutionOutput("D");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("A");
        await context.TriggerAsync("B");
        await context.TriggerAsync("C");
        await context.TriggerAsync("D");
    }
}

/// <summary>
/// Wait All — waits for a set number of triggers before continuing.
/// Useful for synchronization points.
/// </summary>
public sealed class WaitCountNode : NodeBase
{
    private int _triggerCount;

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Wait Count").Category("Flow")
            .Description("Accumulates triggers. Only continues once Count triggers have been received.")
            .Callable()
            .Input<int>("Count", 2)
            .Output<int>("TriggersSoFar");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        _triggerCount++;
        context.SetOutput("TriggersSoFar", _triggerCount);
        if (_triggerCount >= context.GetInput<int>("Count"))
        {
            _triggerCount = 0;
            await context.TriggerAsync("Exit");
        }
    }
}
