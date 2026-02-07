using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Event args for execution feedback messages surfaced from node contexts.
/// </summary>
public sealed class FeedbackMessageEventArgs : EventArgs
{
    public FeedbackMessageEventArgs(string message, NodeData node, ExecutionFeedbackType type, object? tag)
    {
        Message = message;
        Node = node;
        Type = type;
        Tag = tag;
    }

    public string Message { get; }
    public NodeData Node { get; }
    public ExecutionFeedbackType Type { get; }
    public object? Tag { get; }
}
