namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Feedback types used by execution flow control.
/// </summary>
public enum ExecutionFeedbackType
{
    None,
    Break,
    Continue,
    Wait,
    True,
    False
}
