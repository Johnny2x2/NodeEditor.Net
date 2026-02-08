using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed class NodeExecutionEventArgs : EventArgs
{
    public NodeExecutionEventArgs(NodeData node)
    {
        Node = node;
    }

    public NodeData Node { get; }
}
