using System.Collections.ObjectModel;
using System.Linq;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
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
    /// Raised when socket values are updated (e.g., after execution).
    /// </summary>
    public event EventHandler? SocketValuesChanged;

    /// <summary>
    /// Raised when a node execution state changes (executing or error).
    /// </summary>
    public event EventHandler<NodeEventArgs>? NodeExecutionStateChanged;

    /// <summary>
    /// Raised when an undo operation is requested.
    /// Placeholder hook for future history support.
    /// </summary>
    public event EventHandler? UndoRequested;

    /// <summary>
    /// Raised when a redo operation is requested.
    /// Placeholder hook for future history support.
    /// </summary>
    public event EventHandler? RedoRequested;

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

    /// <summary>
    /// Builds a snapshot of node data using the current socket values from view models.
    /// This is useful for execution so inputs edited in the UI are respected.
    /// </summary>
    public IReadOnlyList<NodeData> BuildExecutionNodes()
    {
        return Nodes
            .Select(node => new NodeData(
                node.Data.Id,
                node.Data.Name,
                node.Data.Callable,
                node.Data.ExecInit,
                node.Inputs.Select(socket => socket.Data).ToList(),
                node.Outputs.Select(socket => socket.Data).ToList(),
                node.Data.DefinitionId))
            .ToList();
    }

    /// <summary>
    /// Updates socket values from an execution context and raises <see cref="SocketValuesChanged"/>.
    /// </summary>
    public void ApplyExecutionContext(
        INodeExecutionContext context,
        bool includeInputs = true,
        bool includeOutputs = true,
        bool includeExecutionSockets = false)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        foreach (var node in Nodes)
        {
            if (includeInputs)
            {
                foreach (var input in node.Inputs)
                {
                    if (!includeExecutionSockets && input.Data.IsExecution)
                    {
                        continue;
                    }

                    if (context.TryGetSocketValue(node.Data.Id, input.Data.Name, out var value))
                    {
                        input.SetValue(value);
                    }
                }
            }

            if (includeOutputs)
            {
                foreach (var output in node.Outputs)
                {
                    if (!includeExecutionSockets && output.Data.IsExecution)
                    {
                        continue;
                    }

                    if (context.TryGetSocketValue(node.Data.Id, output.Data.Name, out var value))
                    {
                        output.SetValue(value);
                    }
                }
            }
        }

        SocketValuesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Updates the executing state for a node and raises <see cref="NodeExecutionStateChanged"/>.
    /// </summary>
    public void SetNodeExecuting(string nodeId, bool isExecuting)
    {
        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        if (node.IsExecuting == isExecuting)
        {
            return;
        }

        node.IsExecuting = isExecuting;
        NodeExecutionStateChanged?.Invoke(this, new NodeEventArgs(node));
    }

    /// <summary>
    /// Updates the error state for a node and raises <see cref="NodeExecutionStateChanged"/>.
    /// </summary>
    public void SetNodeError(string nodeId, bool isError)
    {
        var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
        {
            return;
        }

        if (node.IsError == isError)
        {
            return;
        }

        node.IsError = isError;
        NodeExecutionStateChanged?.Invoke(this, new NodeEventArgs(node));
    }

    /// <summary>
    /// Clears execution and error state across all nodes.
    /// </summary>
    public void ResetNodeExecutionState()
    {
        foreach (var node in Nodes)
        {
            var changed = false;

            if (node.IsExecuting)
            {
                node.IsExecuting = false;
                changed = true;
            }

            if (node.IsError)
            {
                node.IsError = false;
                changed = true;
            }

            if (changed)
            {
                NodeExecutionStateChanged?.Invoke(this, new NodeEventArgs(node));
            }
        }
    }

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

        var connectionsToRemove = Connections
            .Where(c => c.OutputNodeId == nodeId || c.InputNodeId == nodeId)
            .ToList();
        foreach (var connection in connectionsToRemove)
        {
            Connections.Remove(connection);
            ConnectionRemoved?.Invoke(this, new ConnectionEventArgs(connection));
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
        // Only create a copy if there are subscribers
        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;

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

        if (previousSelection != null)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, SelectedNodeIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Toggles the selection state of a node and raises the <see cref="SelectionChanged"/> event.
    /// If the node is currently selected, it will be deselected, and vice versa.
    /// </summary>
    /// <param name="nodeId">The ID of the node to toggle.</param>
    public void ToggleSelectNode(string nodeId)
    {
        // Only create a copy if there are subscribers
        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;
        
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

        if (previousSelection != null)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, SelectedNodeIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Clears all selected nodes and raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    public void ClearSelection()
    {
        // Only create a copy if there are subscribers
        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;
        ClearSelectionInternal();
        
        if (previousSelection != null)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, new HashSet<string>()));
        }
    }

    /// <summary>
    /// Selects a set of nodes and raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    /// <param name="nodeIds">The node IDs to select.</param>
    /// <param name="clearExisting">If true, clears existing selection first.</param>
    public void SelectNodes(IEnumerable<string> nodeIds, bool clearExisting = true)
    {
        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;

        if (clearExisting)
        {
            ClearSelectionInternal();
        }

        foreach (var nodeId in nodeIds)
        {
            var node = Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
            if (node is null)
            {
                continue;
            }

            SelectedNodeIds.Add(nodeId);
            node.IsSelected = true;
        }

        if (previousSelection != null)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, SelectedNodeIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Selects all nodes in the graph.
    /// </summary>
    public void SelectAll()
    {
        SelectNodes(Nodes.Select(n => n.Data.Id), clearExisting: true);
    }

    /// <summary>
    /// Removes all selected nodes and their connections.
    /// </summary>
    public void RemoveSelectedNodes()
    {
        var selected = SelectedNodeIds.ToList();
        foreach (var nodeId in selected)
        {
            RemoveNode(nodeId);
        }

        if (selected.Count > 0)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(selected.ToHashSet(), SelectedNodeIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Requests an undo operation.
    /// Placeholder hook for future history support.
    /// </summary>
    public void RequestUndo() => UndoRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Requests a redo operation.
    /// Placeholder hook for future history support.
    /// </summary>
    public void RequestRedo() => RedoRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Clears all nodes, connections, selections, and resets viewport/zoom.
    /// </summary>
    public void Clear()
    {
        var nodesToRemove = Nodes.ToList();
        foreach (var node in nodesToRemove)
        {
            RemoveNode(node.Data.Id);
        }

        ClearSelectionInternal();
        Viewport = new Rect2D(0, 0, 0, 0);
        Zoom = 1.0;
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
