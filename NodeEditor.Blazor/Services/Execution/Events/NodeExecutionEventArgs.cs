using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class NodeExecutionEventArgs : EventArgs
{
    public NodeExecutionEventArgs(NodeData node)
    {
        Node = node;
    }

    public NodeData Node { get; }
}
