using NodeEditor.Blazor.Models;

namespace NodeEditor.Blazor.Components;

public partial class NodeComponent
{
    private Point2D _lastPosition;
    private Size2D _lastSize;
    private bool _lastSelected;
    private string? _lastName;
    private bool _lastCallable;
    private int _lastConnectionSignature;

    protected override bool ShouldRender()
    {
        if (Node is null)
        {
            return true;
        }

        var connectionSignature = ComputeConnectionSignature();
        var shouldRender = Node.Position != _lastPosition ||
                           Node.Size != _lastSize ||
                           Node.IsSelected != _lastSelected ||
                           Node.Data.Name != _lastName ||
                           Node.Data.Callable != _lastCallable ||
                           connectionSignature != _lastConnectionSignature;

        if (shouldRender)
        {
            _lastPosition = Node.Position;
            _lastSize = Node.Size;
            _lastSelected = Node.IsSelected;
            _lastName = Node.Data.Name;
            _lastCallable = Node.Data.Callable;
            _lastConnectionSignature = connectionSignature;
        }

        return shouldRender;
    }

    private int ComputeConnectionSignature()
    {
        if (Connections is null || Connections.Count == 0)
        {
            return 0;
        }

        var hash = new HashCode();

        foreach (var connection in Connections)
        {
            if (connection.InputNodeId != Node.Data.Id && connection.OutputNodeId != Node.Data.Id)
            {
                continue;
            }

            hash.Add(connection.InputNodeId);
            hash.Add(connection.OutputNodeId);
            hash.Add(connection.InputSocketName);
            hash.Add(connection.OutputSocketName);
            hash.Add(connection.IsExecution);
        }

        return hash.ToHashCode();
    }
}