using NodeEditor.Net.Models;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Net.Services;

/// <summary>
/// Event args for node-related state changes.
/// </summary>
public sealed class NodeEventArgs : EventArgs
{
    public NodeEventArgs(NodeViewModel node)
    {
        Node = node;
    }

    public NodeViewModel Node { get; }
}

/// <summary>
/// Event args for connection-related state changes.
/// </summary>
public sealed class ConnectionEventArgs : EventArgs
{
    public ConnectionEventArgs(ConnectionData connection)
    {
        Connection = connection;
    }

    public ConnectionData Connection { get; }
}

/// <summary>
/// Event args for selection changes.
/// </summary>
public sealed class SelectionChangedEventArgs : EventArgs
{
    public SelectionChangedEventArgs(HashSet<string> previousSelection, HashSet<string> currentSelection)
    {
        PreviousSelection = previousSelection;
        CurrentSelection = currentSelection;
    }

    public HashSet<string> PreviousSelection { get; }
    public HashSet<string> CurrentSelection { get; }
}

/// <summary>
/// Event args for connection selection changes.
/// </summary>
public sealed class ConnectionSelectionChangedEventArgs : EventArgs
{
    public ConnectionSelectionChangedEventArgs(ConnectionData? previousSelection, ConnectionData? currentSelection)
    {
        PreviousSelection = previousSelection;
        CurrentSelection = currentSelection;
    }

    public ConnectionData? PreviousSelection { get; }
    public ConnectionData? CurrentSelection { get; }
}

/// <summary>
/// Event args for viewport changes.
/// </summary>
public sealed class ViewportChangedEventArgs : EventArgs
{
    public ViewportChangedEventArgs(Rect2D previousViewport, Rect2D currentViewport)
    {
        PreviousViewport = previousViewport;
        CurrentViewport = currentViewport;
    }

    public Rect2D PreviousViewport { get; }
    public Rect2D CurrentViewport { get; }
}

/// <summary>
/// Event args for zoom level changes.
/// </summary>
public sealed class ZoomChangedEventArgs : EventArgs
{
    public ZoomChangedEventArgs(double previousZoom, double currentZoom)
    {
        PreviousZoom = previousZoom;
        CurrentZoom = currentZoom;
    }

    public double PreviousZoom { get; }
    public double CurrentZoom { get; }
}

/// <summary>
/// Event args for graph variable changes.
/// </summary>
public sealed class GraphVariableEventArgs : EventArgs
{
    public GraphVariableEventArgs(GraphVariable variable)
    {
        Variable = variable;
    }

    public GraphVariable Variable { get; }
}

/// <summary>
/// Event args for graph variable updates (rename, type change, default value change).
/// </summary>
public sealed class GraphVariableChangedEventArgs : EventArgs
{
    public GraphVariableChangedEventArgs(GraphVariable previousVariable, GraphVariable currentVariable)
    {
        PreviousVariable = previousVariable;
        CurrentVariable = currentVariable;
    }

    public GraphVariable PreviousVariable { get; }
    public GraphVariable CurrentVariable { get; }
}

/// <summary>
/// Event args for graph event changes.
/// </summary>
public sealed class GraphEventEventArgs : EventArgs
{
    public GraphEventEventArgs(GraphEvent @event)
    {
        Event = @event;
    }

    public GraphEvent Event { get; }
}

/// <summary>
/// Event args for graph event updates (rename).
/// </summary>
public sealed class GraphEventChangedEventArgs : EventArgs
{
    public GraphEventChangedEventArgs(GraphEvent previousEvent, GraphEvent currentEvent)
    {
        PreviousEvent = previousEvent;
        CurrentEvent = currentEvent;
    }

    public GraphEvent PreviousEvent { get; }
    public GraphEvent CurrentEvent { get; }
}

/// <summary>
/// Event args for overlay-related state changes.
/// </summary>
public sealed class OverlayEventArgs : EventArgs
{
    public OverlayEventArgs(OverlayViewModel overlay)
    {
        Overlay = overlay;
    }

    public OverlayViewModel Overlay { get; }
}

/// <summary>
/// Event args for overlay selection changes.
/// </summary>
public sealed class OverlaySelectionChangedEventArgs : EventArgs
{
    public OverlaySelectionChangedEventArgs(HashSet<string> previousSelection, HashSet<string> currentSelection)
    {
        PreviousSelection = previousSelection;
        CurrentSelection = currentSelection;
    }

    public HashSet<string> PreviousSelection { get; }
    public HashSet<string> CurrentSelection { get; }
}
