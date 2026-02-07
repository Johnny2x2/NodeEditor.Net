using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext
{
    /// <summary>
    /// Callable node that prints a labeled debug message to the Debug output channel.
    /// Appears in the node context menu under Debug.
    /// </summary>
    [Node("Debug Print", "Debug", "Debug", "Print a labeled debug message to the output terminal.", true)]
    public void DebugPrint(ExecutionPath Enter, string Label, object? Value, out ExecutionPath Exit)
    {
        var label = Label ?? "Debug";
        var valueStr = Value?.ToString() ?? "(null)";
        var message = $"[{label}] {valueStr}";

        if (CurrentProcessingNode is not null)
        {
            FeedbackInfo?.Invoke(message, CurrentProcessingNode, ExecutionFeedbackType.DebugPrint, Value, false);
        }

        Exit = new ExecutionPath();
        Exit.Signal();
    }

    /// <summary>
    /// Pure (non-callable) node that prints a value to the Debug output channel.
    /// Evaluates as a data node — no exec wiring needed.
    /// </summary>
    [Node("Print Value", "Debug", "Debug", "Print a value to the output terminal (data-flow).", false)]
    public void PrintValue(string Label, object? Value, out object? PassThrough)
    {
        var label = Label ?? "Print";
        var valueStr = Value?.ToString() ?? "(null)";
        var message = $"[{label}] {valueStr}";

        if (CurrentProcessingNode is not null)
        {
            FeedbackInfo?.Invoke(message, CurrentProcessingNode, ExecutionFeedbackType.DebugPrint, Value, false);
        }

        PassThrough = Value;
    }

    /// <summary>
    /// Callable node that logs a warning message to the Debug output channel.
    /// </summary>
    [Node("Debug Warning", "Debug", "Debug", "Print a warning message to the output terminal.", true)]
    public void DebugWarning(ExecutionPath Enter, string Message, out ExecutionPath Exit)
    {
        var msg = Message ?? "(empty warning)";
        var fullMessage = $"⚠ {msg}";

        if (CurrentProcessingNode is not null)
        {
            FeedbackInfo?.Invoke(fullMessage, CurrentProcessingNode, ExecutionFeedbackType.DebugPrint, null, false);
        }

        Exit = new ExecutionPath();
        Exit.Signal();
    }

    /// <summary>
    /// Callable node that logs an error message to the Debug output channel.
    /// Does not halt execution — just emits an error-level message.
    /// </summary>
    [Node("Debug Error", "Debug", "Debug", "Print an error message to the output terminal.", true)]
    public void DebugError(ExecutionPath Enter, string Message, out ExecutionPath Exit)
    {
        var msg = Message ?? "(empty error)";
        var fullMessage = $"✕ {msg}";

        if (CurrentProcessingNode is not null)
        {
            FeedbackInfo?.Invoke(fullMessage, CurrentProcessingNode, ExecutionFeedbackType.DebugPrint, null, false);
        }

        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
