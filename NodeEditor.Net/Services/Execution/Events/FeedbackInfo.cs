namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Feedback info used for execution flow control.
/// </summary>
public readonly record struct FeedbackInfo(ExecutionFeedbackType Type, object? Value = null);
