using System.Collections.ObjectModel;
using System.Linq;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

public sealed class NodeEditorState
{
    // Events for state changes
    public event EventHandler<NodeEventArgs>? NodeAdded;
    public event EventHandler<NodeEventArgs>? NodeRemoved;
    public event EventHandler<ConnectionEventArgs>? ConnectionAdded;
    public event EventHandler<ConnectionEventArgs>? ConnectionRemoved;
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    public ObservableCollection<ConnectionData> Connections { get; } = new();

    public HashSet<string> SelectedNodeIds { get; } = new();

    private double _zoom = 1.0;
    public double Zoom
    {
        get => _zoom;
        set
        {
            if (Math.Abs(_zoom - value) > double.Epsilon)
            {
                var oldZoom = _zoom;
                _zoom = value;
                ZoomChanged?.Invoke(this, new ZoomChangedEventArgs(oldZoom, value));
            }
        }
    }

    private Rect2D _viewport = new(0, 0, 0, 0);
    public Rect2D Viewport
    {
        get => _viewport;
        set
        {
            if (_viewport != value)
            {
                var oldViewport = _viewport;
                _viewport = value;
                ViewportChanged?.Invoke(this, new ViewportChangedEventArgs(oldViewport, value));
            }
        }
    }

    public void AddNode(NodeViewModel node)
    {
        Nodes.Add(node);
        NodeAdded?.Invoke(this, new NodeEventArgs(node));
    }

    public void RemoveNode(string nodeId)
    {
        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        Nodes.Remove(node);
        SelectedNodeIds.Remove(nodeId);
        NodeRemoved?.Invoke(this, new NodeEventArgs(node));
    }

    public void AddConnection(ConnectionData connection)
    {
        Connections.Add(connection);
        ConnectionAdded?.Invoke(this, new ConnectionEventArgs(connection));
    }

    public void RemoveConnection(ConnectionData connection)
    {
        Connections.Remove(connection);
        ConnectionRemoved?.Invoke(this, new ConnectionEventArgs(connection));
    }

    public void SelectNode(string nodeId, bool clearExisting = true)
    {
        var previousSelection = SelectedNodeIds.ToHashSet();

        if (clearExisting)
        {
            ClearSelectionInternal();
        }

        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        SelectedNodeIds.Add(nodeId);
        node.IsSelected = true;

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, SelectedNodeIds.ToHashSet()));
    }

    public void ToggleSelectNode(string nodeId)
    {
        var previousSelection = SelectedNodeIds.ToHashSet();
        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        if (SelectedNodeIds.Contains(nodeId))
        {
            SelectedNodeIds.Remove(nodeId);
            node.IsSelected = false;
        }
        else
        {
            SelectedNodeIds.Add(nodeId);
            node.IsSelected = true;
        }

        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, SelectedNodeIds.ToHashSet()));
    }

    public void ClearSelection()
    {
        var previousSelection = SelectedNodeIds.ToHashSet();
        ClearSelectionInternal();
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, new HashSet<string>()));
    }

    private void ClearSelectionInternal()
    {
        SelectedNodeIds.Clear();
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }
}
