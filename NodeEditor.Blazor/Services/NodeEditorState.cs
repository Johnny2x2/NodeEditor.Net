using System.Collections.ObjectModel;
using System.Linq;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Central state management for the node editor using an event-based architecture.
/// This class provides a single source of truth for the node graph state and raises
/// events when state changes occur, enabling reactive Blazor UI updates.
/// </summary>
/// <remarks>
/// Event-based architecture benefits:
/// - Blazor components can subscribe to specific state changes for efficient rendering
/// - Follows the observer pattern for loose coupling
/// - Enables performance optimizations by avoiding unnecessary re-renders
/// - Supports undo/redo and history tracking in future implementations
/// 
/// Usage in Blazor components:
/// <code>
/// protected override void OnInitialized()
/// {
///     EditorState.NodeAdded += OnNodeAdded;
///     EditorState.SelectionChanged += OnSelectionChanged;
/// }
/// 
/// public void Dispose()
/// {
///     EditorState.NodeAdded -= OnNodeAdded;
///     EditorState.SelectionChanged -= OnSelectionChanged;
/// }
/// 
/// private void OnNodeAdded(object? sender, NodeEventArgs e)
/// {
///     StateHasChanged(); // Trigger Blazor re-render
/// }
/// </code>
/// </remarks>
public sealed class NodeEditorState
{
    // Events for state changes
    
    /// <summary>
    /// Raised when a node is added to the graph.
    /// </summary>
    public event EventHandler<NodeEventArgs>? NodeAdded;
    
    /// <summary>
    /// Raised when a node is removed from the graph.
    /// </summary>
    public event EventHandler<NodeEventArgs>? NodeRemoved;
    
    /// <summary>
    /// Raised when a connection is added to the graph.
    /// </summary>
    public event EventHandler<ConnectionEventArgs>? ConnectionAdded;
    
    /// <summary>
    /// Raised when a connection is removed from the graph.
    /// </summary>
    public event EventHandler<ConnectionEventArgs>? ConnectionRemoved;
    
    /// <summary>
    /// Raised when the selection state changes (nodes selected or deselected).
    /// </summary>
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    
    /// <summary>
    /// Raised when the viewport (visible area of the canvas) changes.
    /// </summary>
    public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
    
    /// <summary>
    /// Raised when the zoom level changes.
    /// </summary>
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    /// <summary>
    /// Gets the collection of all nodes in the editor.
    /// Note: Use AddNode() and RemoveNode() methods instead of modifying this collection directly
    /// to ensure events are raised properly.
    /// </summary>
    public ObservableCollection<NodeViewModel> Nodes { get; } = new();
    
    /// <summary>
    /// Gets the collection of all connections between nodes.
    /// Note: Use AddConnection() and RemoveConnection() methods instead of modifying this collection
    /// directly to ensure events are raised properly.
    /// </summary>
    public ObservableCollection<ConnectionData> Connections { get; } = new();

    /// <summary>
    /// Gets the set of IDs for currently selected nodes.
    /// </summary>
    public HashSet<string> SelectedNodeIds { get; } = new();

    private double _zoom = 1.0;
    
    /// <summary>
    /// Gets or sets the current zoom level (1.0 = 100%).
    /// Raises the <see cref="ZoomChanged"/> event when modified.
    /// </summary>
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
    
    /// <summary>
    /// Gets or sets the current viewport (visible area on the canvas).
    /// Raises the <see cref="ViewportChanged"/> event when modified.
    /// </summary>
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

    /// <summary>
    /// Adds a node to the graph and raises the <see cref="NodeAdded"/> event.
    /// </summary>
    /// <param name="node">The node to add.</param>
    public void AddNode(NodeViewModel node)
    {
        Nodes.Add(node);
        NodeAdded?.Invoke(this, new NodeEventArgs(node));
    }

    /// <summary>
    /// Removes a node from the graph by ID and raises the <see cref="NodeRemoved"/> event.
    /// Also removes the node from the current selection if selected.
    /// </summary>
    /// <param name="nodeId">The ID of the node to remove.</param>
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

    /// <summary>
    /// Adds a connection to the graph and raises the <see cref="ConnectionAdded"/> event.
    /// </summary>
    /// <param name="connection">The connection to add.</param>
    public void AddConnection(ConnectionData connection)
    {
        Connections.Add(connection);
        ConnectionAdded?.Invoke(this, new ConnectionEventArgs(connection));
    }

    /// <summary>
    /// Removes a connection from the graph and raises the <see cref="ConnectionRemoved"/> event.
    /// </summary>
    /// <param name="connection">The connection to remove.</param>
    public void RemoveConnection(ConnectionData connection)
    {
        Connections.Remove(connection);
        ConnectionRemoved?.Invoke(this, new ConnectionEventArgs(connection));
    }

    /// <summary>
    /// Selects a node by ID and raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    /// <param name="nodeId">The ID of the node to select.</param>
    /// <param name="clearExisting">If true, clears the existing selection before selecting the node.</param>
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

    /// <summary>
    /// Toggles the selection state of a node and raises the <see cref="SelectionChanged"/> event.
    /// If the node is currently selected, it will be deselected, and vice versa.
    /// </summary>
    /// <param name="nodeId">The ID of the node to toggle.</param>
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

    /// <summary>
    /// Clears all selected nodes and raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    public void ClearSelection()
    {
        var previousSelection = SelectedNodeIds.ToHashSet();
        ClearSelectionInternal();
        SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, new HashSet<string>()));
    }

    /// <summary>
    /// Internal method to clear selection without raising events.
    /// </summary>
    private void ClearSelectionInternal()
    {
        SelectedNodeIds.Clear();
        foreach (var node in Nodes)
        {
            node.IsSelected = false;
        }
    }
}
