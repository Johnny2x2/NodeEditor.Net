using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

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
