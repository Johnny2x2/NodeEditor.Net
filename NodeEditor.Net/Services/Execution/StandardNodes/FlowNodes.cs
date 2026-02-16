namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Sequence node — triggers multiple execution outputs in order.
/// Useful for orchestrating multi-step flows from a single point.
/// </summary>
public sealed class SequenceNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Sequence").Category("Flow")
            .Description("Triggers multiple execution outputs sequentially.")
            .Callable()
            .ExecutionOutput("Then 0")
            .ExecutionOutput("Then 1")
            .ExecutionOutput("Then 2")
            .ExecutionOutput("Then 3");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Then 0");
        await context.TriggerAsync("Then 1");
        await context.TriggerAsync("Then 2");
        await context.TriggerAsync("Then 3");
    }
}

/// <summary>
/// Switch on an integer value to branch into up to 5 cases plus a default.
/// </summary>
public sealed class SwitchIntNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Switch Int").Category("Flow")
            .Description("Branches execution based on an integer value (0–4), with a Default fallback.")
            .Callable()
            .Input<int>("Value", 0)
            .ExecutionOutput("Case 0")
            .ExecutionOutput("Case 1")
            .ExecutionOutput("Case 2")
            .ExecutionOutput("Case 3")
            .ExecutionOutput("Case 4")
            .ExecutionOutput("Default");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var value = context.GetInput<int>("Value");
        var caseName = value switch
        {
            0 => "Case 0",
            1 => "Case 1",
            2 => "Case 2",
            3 => "Case 3",
            4 => "Case 4",
            _ => "Default"
        };
        await context.TriggerAsync(caseName);
    }
}

/// <summary>
/// Switch on a string value to branch into up to 4 named cases plus a default.
/// </summary>
public sealed class SwitchStringNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Switch String").Category("Flow")
            .Description("Branches execution based on a string matching one of 4 case values.")
            .Callable()
            .Input<string>("Value", "")
            .Input<string>("Match 0", "")
            .Input<string>("Match 1", "")
            .Input<string>("Match 2", "")
            .Input<string>("Match 3", "")
            .ExecutionOutput("Case 0")
            .ExecutionOutput("Case 1")
            .ExecutionOutput("Case 2")
            .ExecutionOutput("Case 3")
            .ExecutionOutput("Default");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var value = context.GetInput<string>("Value") ?? "";
        for (int i = 0; i < 4; i++)
        {
            if (string.Equals(value, context.GetInput<string>($"Match {i}"), StringComparison.Ordinal))
            {
                await context.TriggerAsync($"Case {i}");
                return;
            }
        }
        await context.TriggerAsync("Default");
    }
}

/// <summary>
/// Gate node — only continues if the condition is true, otherwise takes the Else path.
/// Unlike Branch which always takes one path, Gate stops flow if closed.
/// </summary>
public sealed class GateNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Gate").Category("Flow")
            .Description("Continues execution only if the condition is true. Otherwise takes the Closed path (if connected).")
            .Callable()
            .Input<bool>("Open", true)
            .ExecutionOutput("Continue")
            .ExecutionOutput("Closed");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        if (context.GetInput<bool>("Open"))
            await context.TriggerAsync("Continue");
        else
            await context.TriggerAsync("Closed");
    }
}

/// <summary>
/// Once node — only executes its body the first time it's triggered.
/// Subsequent triggers pass through to "Already Run" output.
/// </summary>
public sealed class OnceNode : NodeBase
{
    private bool _hasRun;

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Once").Category("Flow")
            .Description("Executes the main path only on the first trigger. Subsequent triggers go to 'Already Run'.")
            .Callable()
            .ExecutionOutput("First")
            .ExecutionOutput("Already Run");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        if (!_hasRun)
        {
            _hasRun = true;
            await context.TriggerAsync("First");
        }
        else
        {
            await context.TriggerAsync("Already Run");
        }
    }
}

/// <summary>
/// Counter node — counts how many times it's been triggered.
/// </summary>
public sealed class CounterNode : NodeBase
{
    private int _count;

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Counter").Category("Flow")
            .Description("Counts how many times it's been triggered. Outputs the count and continues.")
            .Callable()
            .Input<bool>("Reset")
            .Output<int>("Count");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        if (context.GetInput<bool>("Reset"))
            _count = 0;
        _count++;
        context.SetOutput("Count", _count);
        await context.TriggerAsync("Exit");
    }
}

/// <summary>
/// Flip Flop — alternates between two execution paths on each trigger.
/// </summary>
public sealed class FlipFlopNode : NodeBase
{
    private bool _state;

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Flip Flop").Category("Flow")
            .Description("Alternates between path A and B on each trigger.")
            .Callable()
            .Output<bool>("IsA")
            .ExecutionOutput("A")
            .ExecutionOutput("B");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        _state = !_state;
        context.SetOutput("IsA", _state);
        await context.TriggerAsync(_state ? "A" : "B");
    }
}
