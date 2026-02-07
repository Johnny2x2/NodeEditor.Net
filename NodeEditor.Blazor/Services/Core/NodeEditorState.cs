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
public class NodeEditorState : INodeEditorState
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
    /// Raised when the connection selection changes.
    /// </summary>
    public event EventHandler<ConnectionSelectionChangedEventArgs>? ConnectionSelectionChanged;
    
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
    /// Raised when a graph variable is added.
    /// </summary>
    public event EventHandler<GraphVariableEventArgs>? VariableAdded;

    /// <summary>
    /// Raised when a graph variable is removed.
    /// </summary>
    public event EventHandler<GraphVariableEventArgs>? VariableRemoved;

    /// <summary>
    /// Raised when a graph variable is updated (name, type, or default value changed).
    /// </summary>
    public event EventHandler<GraphVariableChangedEventArgs>? VariableChanged;

    /// <summary>
    /// Raised when a graph event is added.
    /// </summary>
    public event EventHandler<GraphEventEventArgs>? EventAdded;

    /// <summary>
    /// Raised when a graph event is removed.
    /// </summary>
    public event EventHandler<GraphEventEventArgs>? EventRemoved;

    /// <summary>
    /// Raised when a graph event is updated (renamed).
    /// </summary>
    public event EventHandler<GraphEventChangedEventArgs>? EventChanged;

    /// <summary>
    /// Raised when an overlay is added.
    /// </summary>
    public event EventHandler<OverlayEventArgs>? OverlayAdded;

    /// <summary>
    /// Raised when an overlay is removed.
    /// </summary>
    public event EventHandler<OverlayEventArgs>? OverlayRemoved;

    /// <summary>
    /// Raised when overlay selection changes.
    /// </summary>
    public event EventHandler<OverlaySelectionChangedEventArgs>? OverlaySelectionChanged;

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
    /// Gets the collection of all graph variables.
    /// Note: Use AddVariable(), RemoveVariable(), and UpdateVariable() methods instead of
    /// modifying this collection directly to ensure events are raised properly.
    /// </summary>
    public ObservableCollection<GraphVariable> Variables { get; } = new();

    /// <summary>
    /// Gets the collection of all graph events.
    /// Note: Use AddEvent(), RemoveEvent(), and UpdateEvent() methods instead of
    /// modifying this collection directly to ensure events are raised properly.
    /// </summary>
    public ObservableCollection<GraphEvent> Events { get; } = new();

    /// <summary>
    /// Gets the collection of all organizer overlays.
    /// </summary>
    public ObservableCollection<OverlayViewModel> Overlays { get; } = new();

    /// <summary>
    /// Gets the set of IDs for currently selected nodes.
    /// </summary>
    public HashSet<string> SelectedNodeIds { get; } = new();

    /// <summary>
    /// Gets the set of IDs for currently selected overlays.
    /// </summary>
    public HashSet<string> SelectedOverlayIds { get; } = new();

    /// <summary>
    /// Gets the currently selected connection, if any.
    /// </summary>
    public ConnectionData? SelectedConnection { get; private set; }

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
    /// Exports the current UI state to a pure graph model with layout data.
    /// </summary>
    public GraphData ExportToGraphData()
    {
        var nodes = Nodes.Select(vm => new GraphNodeData(
            new NodeData(
                vm.Data.Id,
                vm.Data.Name,
                vm.Data.Callable,
                vm.Data.ExecInit,
                vm.Inputs.Select(socket => socket.Data).ToList(),
                vm.Outputs.Select(socket => socket.Data).ToList(),
                vm.Data.DefinitionId),
            vm.Position,
            vm.Size)).ToList();

        var overlays = Overlays.Select(overlay => new OverlayData(
            overlay.Id,
            overlay.Title,
            overlay.Body,
            overlay.Position,
            overlay.Size,
            overlay.Color,
            overlay.Opacity)).ToList();

        return new GraphData(nodes, Connections.ToList(), Variables.ToList(), Events.ToList(), overlays);
    }

    /// <summary>
    /// Loads a pure graph model into UI state (creates view models).
    /// </summary>
    public void LoadFromGraphData(GraphData graphData)
    {
        if (graphData is null)
        {
            throw new ArgumentNullException(nameof(graphData));
        }

        Clear();

        foreach (var variable in graphData.Variables)
        {
            AddVariable(variable);
        }

        if (graphData.Events is not null)
        {
            foreach (var graphEvent in graphData.Events)
            {
                AddEvent(graphEvent);
            }
        }

        foreach (var graphNode in graphData.Nodes)
        {
            var vm = new NodeViewModel(graphNode.Data)
            {
                Position = graphNode.Position,
                Size = graphNode.Size,
                IsSelected = false
            };
            AddNode(vm);
        }

        foreach (var connection in graphData.Connections)
        {
            AddConnection(connection);
        }

        if (graphData.Overlays is not null)
        {
            foreach (var overlay in graphData.Overlays)
            {
                AddOverlay(new OverlayViewModel(overlay));
            }
        }
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
    public NodeViewModel AddNode(NodeViewModel node)
    {
        Nodes.Add(node);
        NodeAdded?.Invoke(this, new NodeEventArgs(node));
        return node;
    }

    /// <summary>
    /// Adds an overlay to the graph and raises the <see cref="OverlayAdded"/> event.
    /// </summary>
    public OverlayViewModel AddOverlay(OverlayViewModel overlay)
    {
        Overlays.Add(overlay);
        OverlayAdded?.Invoke(this, new OverlayEventArgs(overlay));
        return overlay;
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

        RemoveConnectionsToNode(nodeId);

        Nodes.Remove(node);
        SelectedNodeIds.Remove(nodeId);
        NodeRemoved?.Invoke(this, new NodeEventArgs(node));
    }

    /// <summary>
    /// Removes an overlay from the graph by ID and raises the <see cref="OverlayRemoved"/> event.
    /// </summary>
    public void RemoveOverlay(string overlayId)
    {
        var overlay = Overlays.FirstOrDefault(o => o.Id == overlayId);
        if (overlay is null)
        {
            return;
        }

        Overlays.Remove(overlay);
        SelectedOverlayIds.Remove(overlayId);
        OverlayRemoved?.Invoke(this, new OverlayEventArgs(overlay));
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
        if (SelectedConnection == connection)
        {
            ClearConnectionSelection();
        }

        Connections.Remove(connection);
        ConnectionRemoved?.Invoke(this, new ConnectionEventArgs(connection));
    }

    /// <summary>
    /// Removes all connections that touch a node (input or output).
    /// </summary>
    /// <param name="nodeId">The ID of the node whose connections should be removed.</param>
    public void RemoveConnectionsToNode(string nodeId)
    {
        var connectionsToRemove = Connections
            .Where(c => c.OutputNodeId == nodeId || c.InputNodeId == nodeId)
            .ToList();

        foreach (var connection in connectionsToRemove)
        {
            RemoveConnection(connection);
        }
    }

    /// <summary>
    /// Removes all connections to a specific input socket.
    /// </summary>
    /// <param name="nodeId">The ID of the node with the input socket.</param>
    /// <param name="socketName">The name of the input socket.</param>
    public void RemoveConnectionsToInput(string nodeId, string socketName)
    {
        var connectionsToRemove = Connections
            .Where(c => c.InputNodeId == nodeId && c.InputSocketName == socketName)
            .ToList();

        foreach (var connection in connectionsToRemove)
        {
            RemoveConnection(connection);
        }
    }

    /// <summary>
    /// Removes all connections from a specific output socket.
    /// </summary>
    /// <param name="nodeId">The ID of the node with the output socket.</param>
    /// <param name="socketName">The name of the output socket.</param>
    public void RemoveConnectionsFromOutput(string nodeId, string socketName)
    {
        var connectionsToRemove = Connections
            .Where(c => c.OutputNodeId == nodeId && c.OutputSocketName == socketName)
            .ToList();

        foreach (var connection in connectionsToRemove)
        {
            RemoveConnection(connection);
        }
    }

    /// <summary>
    /// Selects a connection and raises <see cref="ConnectionSelectionChanged"/>.
    /// </summary>
    /// <param name="connection">The connection to select.</param>
    /// <param name="clearNodeSelection">If true, clears any selected nodes first.</param>
    public void SelectConnection(ConnectionData connection, bool clearNodeSelection = true)
    {
        if (clearNodeSelection && (SelectedNodeIds.Count > 0 || SelectedOverlayIds.Count > 0))
        {
            ClearSelection();
        }

        if (SelectedConnection == connection)
        {
            return;
        }

        var previous = SelectedConnection;
        SelectedConnection = connection;
        ConnectionSelectionChanged?.Invoke(this, new ConnectionSelectionChangedEventArgs(previous, SelectedConnection));
    }

    /// <summary>
    /// Clears the selected connection and raises <see cref="ConnectionSelectionChanged"/>.
    /// </summary>
    public void ClearConnectionSelection()
    {
        if (SelectedConnection is null)
        {
            return;
        }

        var previous = SelectedConnection;
        SelectedConnection = null;
        ConnectionSelectionChanged?.Invoke(this, new ConnectionSelectionChangedEventArgs(previous, null));
    }

    /// <summary>
    /// Selects a node by ID and raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    /// <param name="nodeId">The ID of the node to select.</param>
    /// <param name="clearExisting">If true, clears the existing selection before selecting the node.</param>
    public void SelectNode(string nodeId, bool clearExisting = true)
    {
        ClearConnectionSelection();

        // Only create a copy if there are subscribers
        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;

        if (clearExisting)
        {
            ClearSelectionInternal();
            ClearOverlaySelectionInternal();
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
        ClearConnectionSelection();

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
        ClearConnectionSelection();

        // Only create a copy if there are subscribers
        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;
        var previousOverlaySelection = OverlaySelectionChanged != null ? SelectedOverlayIds.ToHashSet() : null;

        ClearSelectionInternal();
        ClearOverlaySelectionInternal();
        
        if (previousSelection != null)
        {
            SelectionChanged?.Invoke(this, new SelectionChangedEventArgs(previousSelection, new HashSet<string>()));
        }

        if (previousOverlaySelection != null)
        {
            OverlaySelectionChanged?.Invoke(this, new OverlaySelectionChangedEventArgs(previousOverlaySelection, new HashSet<string>()));
        }
    }

    /// <summary>
    /// Selects a set of nodes and raises the <see cref="SelectionChanged"/> event.
    /// </summary>
    /// <param name="nodeIds">The node IDs to select.</param>
    /// <param name="clearExisting">If true, clears existing selection first.</param>
    public void SelectNodes(IEnumerable<string> nodeIds, bool clearExisting = true)
    {
        ClearConnectionSelection();

        var previousSelection = SelectionChanged != null ? SelectedNodeIds.ToHashSet() : null;

        if (clearExisting)
        {
            ClearSelectionInternal();
            ClearOverlaySelectionInternal();
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
        SelectOverlays(Overlays.Select(o => o.Id), clearExisting: false);
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
    /// Selects an overlay by ID and raises the <see cref="OverlaySelectionChanged"/> event.
    /// </summary>
    public void SelectOverlay(string overlayId, bool clearExisting = true)
    {
        ClearConnectionSelection();

        var previousSelection = OverlaySelectionChanged != null ? SelectedOverlayIds.ToHashSet() : null;

        if (clearExisting)
        {
            ClearOverlaySelectionInternal();
            ClearSelectionInternal();
        }

        var overlay = Overlays.FirstOrDefault(o => o.Id == overlayId);
        if (overlay is null)
        {
            return;
        }

        SelectedOverlayIds.Add(overlayId);
        overlay.IsSelected = true;

        if (previousSelection != null)
        {
            OverlaySelectionChanged?.Invoke(this, new OverlaySelectionChangedEventArgs(previousSelection, SelectedOverlayIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Toggles the selection state of an overlay and raises the <see cref="OverlaySelectionChanged"/> event.
    /// </summary>
    public void ToggleSelectOverlay(string overlayId)
    {
        ClearConnectionSelection();

        var previousSelection = OverlaySelectionChanged != null ? SelectedOverlayIds.ToHashSet() : null;

        var overlay = Overlays.FirstOrDefault(o => o.Id == overlayId);
        if (overlay is null)
        {
            return;
        }

        if (SelectedOverlayIds.Contains(overlayId))
        {
            SelectedOverlayIds.Remove(overlayId);
            overlay.IsSelected = false;
        }
        else
        {
            SelectedOverlayIds.Add(overlayId);
            overlay.IsSelected = true;
        }

        if (previousSelection != null)
        {
            OverlaySelectionChanged?.Invoke(this, new OverlaySelectionChangedEventArgs(previousSelection, SelectedOverlayIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Clears all selected overlays and raises the <see cref="OverlaySelectionChanged"/> event.
    /// </summary>
    public void ClearOverlaySelection()
    {
        var previousSelection = OverlaySelectionChanged != null ? SelectedOverlayIds.ToHashSet() : null;
        ClearOverlaySelectionInternal();

        if (previousSelection != null)
        {
            OverlaySelectionChanged?.Invoke(this, new OverlaySelectionChangedEventArgs(previousSelection, new HashSet<string>()));
        }
    }

    /// <summary>
    /// Selects a set of overlays and raises the <see cref="OverlaySelectionChanged"/> event.
    /// </summary>
    public void SelectOverlays(IEnumerable<string> overlayIds, bool clearExisting = true)
    {
        var previousSelection = OverlaySelectionChanged != null ? SelectedOverlayIds.ToHashSet() : null;

        if (clearExisting)
        {
            ClearOverlaySelectionInternal();
        }

        foreach (var overlayId in overlayIds)
        {
            var overlay = Overlays.FirstOrDefault(o => o.Id == overlayId);
            if (overlay is null)
            {
                continue;
            }

            SelectedOverlayIds.Add(overlayId);
            overlay.IsSelected = true;
        }

        if (previousSelection != null)
        {
            OverlaySelectionChanged?.Invoke(this, new OverlaySelectionChangedEventArgs(previousSelection, SelectedOverlayIds.ToHashSet()));
        }
    }

    /// <summary>
    /// Removes all selected overlays.
    /// </summary>
    public void RemoveSelectedOverlays()
    {
        var selected = SelectedOverlayIds.ToList();
        foreach (var overlayId in selected)
        {
            RemoveOverlay(overlayId);
        }

        if (selected.Count > 0)
        {
            OverlaySelectionChanged?.Invoke(this, new OverlaySelectionChangedEventArgs(selected.ToHashSet(), SelectedOverlayIds.ToHashSet()));
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

    // ===== Graph Variables =====

    /// <summary>
    /// Adds a graph variable.
    /// </summary>
    public void AddVariable(GraphVariable variable)
    {
        if (variable is null) throw new ArgumentNullException(nameof(variable));
        Variables.Add(variable);
        VariableAdded?.Invoke(this, new GraphVariableEventArgs(variable));
    }

    /// <summary>
    /// Removes a graph variable by its ID. Also removes any Get/Set Variable nodes referencing it.
    /// </summary>
    public void RemoveVariable(string variableId)
    {
        var variable = Variables.FirstOrDefault(v => v.Id == variableId);
        if (variable is null) return;

        // Remove any nodes that reference this variable
        var getDefId = variable.GetDefinitionId;
        var setDefId = variable.SetDefinitionId;
        var nodesToRemove = Nodes
            .Where(n => n.Data.DefinitionId == getDefId || n.Data.DefinitionId == setDefId)
            .Select(n => n.Data.Id)
            .ToList();
        foreach (var nodeId in nodesToRemove)
        {
            RemoveNode(nodeId);
        }

        Variables.Remove(variable);
        VariableRemoved?.Invoke(this, new GraphVariableEventArgs(variable));
    }

    /// <summary>
    /// Updates an existing graph variable (rename, type change, default value).
    /// Existing Get/Set nodes are updated to reflect the new name.
    /// </summary>
    public void UpdateVariable(GraphVariable updated)
    {
        if (updated is null) throw new ArgumentNullException(nameof(updated));

        var index = -1;
        for (var i = 0; i < Variables.Count; i++)
        {
            if (Variables[i].Id == updated.Id)
            {
                index = i;
                break;
            }
        }

        if (index < 0) return;

        var previous = Variables[index];
        Variables[index] = updated;

        // If type changed, disconnect invalid connections on referencing nodes
        if (previous.TypeName != updated.TypeName)
        {
            var getDefId = updated.GetDefinitionId;
            var setDefId = updated.SetDefinitionId;
            var affectedNodes = Nodes
                .Where(n => n.Data.DefinitionId == getDefId || n.Data.DefinitionId == setDefId)
                .ToList();

            foreach (var node in affectedNodes)
            {
                RemoveConnectionsToNode(node.Data.Id);
            }
        }

        // Update node names if renamed by replacing the node VM
        if (previous.Name != updated.Name)
        {
            var getDefId = updated.GetDefinitionId;
            var setDefId = updated.SetDefinitionId;
            var nodesToRebuild = Nodes
                .Where(n => n.Data.DefinitionId == getDefId || n.Data.DefinitionId == setDefId)
                .ToList();

            foreach (var oldNode in nodesToRebuild)
            {
                var isGet = oldNode.Data.DefinitionId == getDefId;
                var newName = isGet ? "Get " + updated.Name : "Set " + updated.Name;
                var newData = oldNode.Data with { Name = newName };
                var newNode = new NodeViewModel(newData)
                {
                    Position = oldNode.Position,
                    Size = oldNode.Size,
                    IsSelected = oldNode.IsSelected
                };

                var idx = Nodes.IndexOf(oldNode);
                if (idx >= 0)
                {
                    Nodes[idx] = newNode;
                }
            }
        }

        VariableChanged?.Invoke(this, new GraphVariableChangedEventArgs(previous, updated));
    }

    /// <summary>
    /// Finds a graph variable by its ID.
    /// </summary>
    public GraphVariable? FindVariable(string variableId)
    {
        return Variables.FirstOrDefault(v => v.Id == variableId);
    }

    // ===== Graph Events =====

    /// <summary>
    /// Adds a graph event.
    /// </summary>
    public void AddEvent(GraphEvent graphEvent)
    {
        if (graphEvent is null) throw new ArgumentNullException(nameof(graphEvent));
        Events.Add(graphEvent);
        EventAdded?.Invoke(this, new GraphEventEventArgs(graphEvent));
    }

    /// <summary>
    /// Removes a graph event by its ID. Also removes any listener/trigger nodes referencing it.
    /// </summary>
    public void RemoveEvent(string eventId)
    {
        var graphEvent = Events.FirstOrDefault(e => e.Id == eventId);
        if (graphEvent is null) return;

        // Remove any nodes that reference this event
        var listenerDefId = graphEvent.ListenerDefinitionId;
        var triggerDefId = graphEvent.TriggerDefinitionId;
        var nodesToRemove = Nodes
            .Where(n => n.Data.DefinitionId == listenerDefId || n.Data.DefinitionId == triggerDefId)
            .Select(n => n.Data.Id)
            .ToList();
        foreach (var nodeId in nodesToRemove)
        {
            RemoveNode(nodeId);
        }

        Events.Remove(graphEvent);
        EventRemoved?.Invoke(this, new GraphEventEventArgs(graphEvent));
    }

    /// <summary>
    /// Updates an existing graph event (rename).
    /// Existing listener/trigger nodes are updated to reflect the new name.
    /// </summary>
    public void UpdateEvent(GraphEvent updated)
    {
        if (updated is null) throw new ArgumentNullException(nameof(updated));

        var index = -1;
        for (var i = 0; i < Events.Count; i++)
        {
            if (Events[i].Id == updated.Id)
            {
                index = i;
                break;
            }
        }

        if (index < 0) return;

        var previous = Events[index];
        Events[index] = updated;

        // Update node names if renamed
        if (previous.Name != updated.Name)
        {
            var listenerDefId = updated.ListenerDefinitionId;
            var triggerDefId = updated.TriggerDefinitionId;
            var nodesToRebuild = Nodes
                .Where(n => n.Data.DefinitionId == listenerDefId || n.Data.DefinitionId == triggerDefId)
                .ToList();

            foreach (var oldNode in nodesToRebuild)
            {
                var isListener = oldNode.Data.DefinitionId == listenerDefId;
                var newName = isListener
                    ? "Custom Event: " + updated.Name
                    : "Trigger Event: " + updated.Name;
                var newData = oldNode.Data with { Name = newName };
                var newNode = new NodeViewModel(newData)
                {
                    Position = oldNode.Position,
                    Size = oldNode.Size,
                    IsSelected = oldNode.IsSelected
                };

                var idx = Nodes.IndexOf(oldNode);
                if (idx >= 0)
                {
                    Nodes[idx] = newNode;
                }
            }
        }

        EventChanged?.Invoke(this, new GraphEventChangedEventArgs(previous, updated));
    }

    /// <summary>
    /// Finds a graph event by its ID.
    /// </summary>
    public GraphEvent? FindEvent(string eventId)
    {
        return Events.FirstOrDefault(e => e.Id == eventId);
    }

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

        var overlaysToRemove = Overlays.ToList();
        foreach (var overlay in overlaysToRemove)
        {
            RemoveOverlay(overlay.Id);
        }

        ClearSelectionInternal();
        ClearOverlaySelectionInternal();
        ClearConnectionSelection();
        Variables.Clear();
        Events.Clear();
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

    /// <summary>
    /// Internal method to clear overlay selection without raising events.
    /// </summary>
    private void ClearOverlaySelectionInternal()
    {
        SelectedOverlayIds.Clear();
        foreach (var overlay in Overlays)
        {
            overlay.IsSelected = false;
        }
    }
}
