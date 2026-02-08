using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed class NodeExecutionFailedEventArgs : EventArgs
{
    public NodeExecutionFailedEventArgs(NodeData node, Exception exception)
    {
        Node = node;
        Exception = exception;
    }

    public NodeData Node { get; }

    public Exception Exception { get; }
}
