namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// TryCatch — wraps execution of the Try path and catches exceptions,
/// routing to Catch with the error message available.
/// </summary>
public sealed class TryCatchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Try Catch").Category("Flow")
            .Description("Executes the Try path. If an exception occurs, executes the Catch path with the error message.")
            .Callable()
            .Output<string>("Error")
            .ExecutionOutput("Try")
            .ExecutionOutput("Catch")
            .ExecutionOutput("Finally");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        try
        {
            context.SetOutput("Error", "");
            await context.TriggerAsync("Try");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            context.SetOutput("Error", ex.Message);
            await context.TriggerAsync("Catch");
        }
        finally
        {
            await context.TriggerAsync("Finally");
        }
    }
}

/// <summary>
/// Throw Error — deliberately throws an exception to propagate up to a TryCatch node.
/// </summary>
public sealed class ThrowErrorNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Throw Error").Category("Flow")
            .Description("Throws an exception with the given message. Use inside a Try-Catch block.")
            .Callable()
            .Input<string>("Message", "An error occurred");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message") ?? "An error occurred";
        throw new InvalidOperationException(message);
    }
}

/// <summary>
/// Assert — checks a condition and throws an error if false.
/// </summary>
public sealed class AssertNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Assert").Category("Debug")
            .Description("Throws an error if the condition is false.")
            .Callable()
            .Input<bool>("Condition", true)
            .Input<string>("Message", "Assertion failed");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        if (!context.GetInput<bool>("Condition"))
        {
            var message = context.GetInput<string>("Message") ?? "Assertion failed";
            context.EmitFeedback(message, ExecutionFeedbackType.Break);
            throw new InvalidOperationException(message);
        }
        await context.TriggerAsync("Exit");
    }
}

/// <summary>
/// Log — callable node that writes debug output for multiple common types and continues.
/// </summary>
public sealed class LogNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Log").Category("Debug")
            .Description("Logs a message to the debug output and continues execution.")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message") ?? "";
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        await context.TriggerAsync("Exit");
    }
}

/// <summary>
/// Comment — a no-op node used purely for adding notes to the graph.
/// </summary>
public sealed class CommentNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Comment").Category("Helpers")
            .Description("A non-functional annotation node for adding notes to your graph.")
            .Input<string>("Text", "Add your notes here...");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        // No-op — this node does nothing
        return Task.CompletedTask;
    }
}

/// <summary>
/// Breakpoint — pauses execution and emits a feedback message. 
/// Useful for debugging flows.
/// </summary>
public sealed class BreakpointNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Breakpoint").Category("Debug")
            .Description("Emits a breakpoint feedback and continues. Useful for debugging flows.")
            .Callable()
            .Input<string>("Label", "Breakpoint hit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var label = context.GetInput<string>("Label") ?? "Breakpoint hit";
        context.EmitFeedback(label, ExecutionFeedbackType.Break);
        await context.TriggerAsync("Exit");
    }
}

/// <summary>
/// Timer — measures how long the downstream execution takes.
/// </summary>
public sealed class TimerNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Timer").Category("Debug")
            .Description("Measures execution time of the Body path in milliseconds.")
            .Callable()
            .Output<double>("ElapsedMs")
            .ExecutionOutput("Body")
            .ExecutionOutput("Done");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await context.TriggerAsync("Body");
        sw.Stop();
        context.SetOutput("ElapsedMs", sw.Elapsed.TotalMilliseconds);
        await context.TriggerAsync("Done");
    }
}

/// <summary>
/// Stopwatch — accumulates elapsed time across multiple triggers.
/// </summary>
public sealed class StopwatchNode : NodeBase
{
    private readonly System.Diagnostics.Stopwatch _sw = new();

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Stopwatch").Category("Debug")
            .Description("Accumulating stopwatch. Start/Stop/Reset with inputs. Outputs total elapsed ms.")
            .Callable()
            .Input<bool>("Start", true)
            .Input<bool>("Stop")
            .Input<bool>("Reset")
            .Output<double>("ElapsedMs");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        if (context.GetInput<bool>("Reset"))
            _sw.Reset();
        if (context.GetInput<bool>("Start"))
            _sw.Start();
        if (context.GetInput<bool>("Stop"))
            _sw.Stop();
        context.SetOutput("ElapsedMs", _sw.Elapsed.TotalMilliseconds);
        await context.TriggerAsync("Exit");
    }
}
