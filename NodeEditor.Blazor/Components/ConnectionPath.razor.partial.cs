using NodeEditor.Net.Models;

namespace NodeEditor.Blazor.Components;

public partial class ConnectionPath
{
    private Point2D _lastStart;
    private Point2D _lastEnd;
    private bool _lastPending;
    private string? _lastOutputNodeId;
    private string? _lastInputNodeId;
    private string? _lastOutputSocketName;
    private string? _lastInputSocketName;
    private bool _lastIsExecution;

    protected override bool ShouldRender()
    {
        if (Connection is null || Nodes is null)
        {
            return true;
        }

        var (start, end) = GetConnectionEndpoints();
        var shouldRender = start != _lastStart ||
                           end != _lastEnd ||
                           IsPending != _lastPending ||
                           Connection.OutputNodeId != _lastOutputNodeId ||
                           Connection.InputNodeId != _lastInputNodeId ||
                           Connection.OutputSocketName != _lastOutputSocketName ||
                           Connection.InputSocketName != _lastInputSocketName ||
                           Connection.IsExecution != _lastIsExecution;

        if (shouldRender)
        {
            _lastStart = start;
            _lastEnd = end;
            _lastPending = IsPending;
            _lastOutputNodeId = Connection.OutputNodeId;
            _lastInputNodeId = Connection.InputNodeId;
            _lastOutputSocketName = Connection.OutputSocketName;
            _lastInputSocketName = Connection.InputSocketName;
            _lastIsExecution = Connection.IsExecution;
        }

        return shouldRender;
    }
}