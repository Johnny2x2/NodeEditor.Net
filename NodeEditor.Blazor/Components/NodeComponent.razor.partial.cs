using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Components;

public partial class NodeComponent
{
    private Point2D _lastPosition;
    private Size2D _lastSize;
    private bool _lastSelected;
    private bool _lastExecuting;
    private bool _lastError;
    private string? _lastName;
    private bool _lastCallable;
    private int _lastConnectionSignature;
    private int _lastPreviewSignature;

    protected override bool ShouldRender()
    {
        if (Node is null)
        {
            return true;
        }

        var connectionSignature = ComputeConnectionSignature();
        var previewSignature = ComputePreviewSignature();
        var shouldRender = Node.Position != _lastPosition ||
                           Node.Size != _lastSize ||
                           Node.IsSelected != _lastSelected ||
                           Node.IsExecuting != _lastExecuting ||
                           Node.IsError != _lastError ||
                           Node.Data.Name != _lastName ||
                           Node.Data.Callable != _lastCallable ||
                   connectionSignature != _lastConnectionSignature ||
                   previewSignature != _lastPreviewSignature;

        if (shouldRender)
        {
            _lastPosition = Node.Position;
            _lastSize = Node.Size;
            _lastSelected = Node.IsSelected;
            _lastExecuting = Node.IsExecuting;
            _lastError = Node.IsError;
            _lastName = Node.Data.Name;
            _lastCallable = Node.Data.Callable;
            _lastConnectionSignature = connectionSignature;
            _lastPreviewSignature = previewSignature;
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

    private int ComputePreviewSignature()
    {
        var preview = GetPreviewImage();
        if (preview is null)
        {
            return 0;
        }

        var hash = new HashCode();
        hash.Add(preview.DataUrl);
        hash.Add(preview.Width ?? 0);
        hash.Add(preview.Height ?? 0);
        return hash.ToHashCode();
    }
}