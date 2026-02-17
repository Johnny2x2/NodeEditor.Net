using System.Collections.ObjectModel;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.ViewModels;

namespace NodeEditor.Net.Services;

/// <summary>
/// An <see cref="INodeEditorState"/> implementation that delegates every call
/// to the currently attached state via <see cref="INodeEditorStateBridge"/>.
/// Throws <see cref="InvalidOperationException"/> when no editor session is active.
/// This allows ability providers (and any other long-lived singleton consumers) to
/// hold a single reference and always reach the live Blazor-circuit state.
///
/// Mutating operations are automatically dispatched onto the Blazor circuit's
/// synchronization context (via <see cref="INodeEditorStateBridge.InvokeAsync"/>)
/// to prevent "not associated with the Dispatcher" errors when called from
/// external threads such as MCP request handlers.
/// </summary>
public sealed class BridgedNodeEditorState : INodeEditorState
{
    private readonly INodeEditorStateBridge _bridge;

    public BridgedNodeEditorState(INodeEditorStateBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    private INodeEditorState State =>
        _bridge.Current ?? throw new InvalidOperationException(
            "No active editor session. Open the Node Editor in the browser first.");

    // ── Dispatcher helpers ──────────────────────────────────────────────

    /// <summary>
    /// Dispatches a void action onto the Blazor circuit's synchronization context.
    /// If no dispatcher is available (e.g. called from within the Blazor circuit itself),
    /// the action runs synchronously on the current thread.
    /// </summary>
    private void Dispatch(Action action)
    {
        var invokeAsync = _bridge.InvokeAsync;
        if (invokeAsync != null)
        {
            invokeAsync(() => { action(); return Task.CompletedTask; })
                .GetAwaiter().GetResult();
        }
        else
        {
            action();
        }
    }

    /// <summary>
    /// Dispatches a function with a return value onto the Blazor circuit's
    /// synchronization context, blocking until the result is available.
    /// </summary>
    private T Dispatch<T>(Func<T> func)
    {
        var invokeAsync = _bridge.InvokeAsync;
        if (invokeAsync != null)
        {
            T result = default!;
            invokeAsync(() => { result = func(); return Task.CompletedTask; })
                .GetAwaiter().GetResult();
            return result;
        }
        return func();
    }

    // ── Events ──────────────────────────────────────────────────────────
    public event EventHandler<NodeEventArgs>? NodeAdded
    {
        add => State.NodeAdded += value;
        remove => State.NodeAdded -= value;
    }

    public event EventHandler<NodeEventArgs>? NodeRemoved
    {
        add => State.NodeRemoved += value;
        remove => State.NodeRemoved -= value;
    }

    public event EventHandler<ConnectionEventArgs>? ConnectionAdded
    {
        add => State.ConnectionAdded += value;
        remove => State.ConnectionAdded -= value;
    }

    public event EventHandler<ConnectionEventArgs>? ConnectionRemoved
    {
        add => State.ConnectionRemoved += value;
        remove => State.ConnectionRemoved -= value;
    }

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged
    {
        add => State.SelectionChanged += value;
        remove => State.SelectionChanged -= value;
    }

    public event EventHandler<ConnectionSelectionChangedEventArgs>? ConnectionSelectionChanged
    {
        add => State.ConnectionSelectionChanged += value;
        remove => State.ConnectionSelectionChanged -= value;
    }

    public event EventHandler<ViewportChangedEventArgs>? ViewportChanged
    {
        add => State.ViewportChanged += value;
        remove => State.ViewportChanged -= value;
    }

    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged
    {
        add => State.ZoomChanged += value;
        remove => State.ZoomChanged -= value;
    }

    public event EventHandler? SocketValuesChanged
    {
        add => State.SocketValuesChanged += value;
        remove => State.SocketValuesChanged -= value;
    }

    public event EventHandler<NodeEventArgs>? NodeExecutionStateChanged
    {
        add => State.NodeExecutionStateChanged += value;
        remove => State.NodeExecutionStateChanged -= value;
    }

    public event EventHandler? UndoRequested
    {
        add => State.UndoRequested += value;
        remove => State.UndoRequested -= value;
    }

    public event EventHandler? RedoRequested
    {
        add => State.RedoRequested += value;
        remove => State.RedoRequested -= value;
    }

    public event EventHandler<GraphVariableEventArgs>? VariableAdded
    {
        add => State.VariableAdded += value;
        remove => State.VariableAdded -= value;
    }

    public event EventHandler<GraphVariableEventArgs>? VariableRemoved
    {
        add => State.VariableRemoved += value;
        remove => State.VariableRemoved -= value;
    }

    public event EventHandler<GraphVariableChangedEventArgs>? VariableChanged
    {
        add => State.VariableChanged += value;
        remove => State.VariableChanged -= value;
    }

    public event EventHandler<GraphEventEventArgs>? EventAdded
    {
        add => State.EventAdded += value;
        remove => State.EventAdded -= value;
    }

    public event EventHandler<GraphEventEventArgs>? EventRemoved
    {
        add => State.EventRemoved += value;
        remove => State.EventRemoved -= value;
    }

    public event EventHandler<GraphEventChangedEventArgs>? EventChanged
    {
        add => State.EventChanged += value;
        remove => State.EventChanged -= value;
    }

    public event EventHandler<OverlayEventArgs>? OverlayAdded
    {
        add => State.OverlayAdded += value;
        remove => State.OverlayAdded -= value;
    }

    public event EventHandler<OverlayEventArgs>? OverlayRemoved
    {
        add => State.OverlayRemoved += value;
        remove => State.OverlayRemoved -= value;
    }

    public event EventHandler<OverlaySelectionChangedEventArgs>? OverlaySelectionChanged
    {
        add => State.OverlaySelectionChanged += value;
        remove => State.OverlaySelectionChanged -= value;
    }

    // ── Collections ─────────────────────────────────────────────────────
    public ObservableCollection<NodeViewModel> Nodes => State.Nodes;
    public ObservableCollection<ConnectionData> Connections => State.Connections;
    public ObservableCollection<GraphVariable> Variables => State.Variables;
    public ObservableCollection<GraphEvent> Events => State.Events;
    public ObservableCollection<OverlayViewModel> Overlays => State.Overlays;
    public HashSet<string> SelectedNodeIds => State.SelectedNodeIds;
    public HashSet<string> SelectedOverlayIds => State.SelectedOverlayIds;
    public ConnectionData? SelectedConnection => State.SelectedConnection;

    // ── Viewport ────────────────────────────────────────────────────────
    public double Zoom { get => State.Zoom; set => Dispatch(() => State.Zoom = value); }
    public Rect2D Viewport { get => State.Viewport; set => Dispatch(() => State.Viewport = value); }

    // ── Execution bridge ────────────────────────────────────────────────
    public IReadOnlyList<NodeData> BuildExecutionNodes() => State.BuildExecutionNodes();
    public void ApplyExecutionContext(INodeRuntimeStorage context, bool includeInputs = true, bool includeOutputs = true, bool includeExecutionSockets = false)
        => Dispatch(() => State.ApplyExecutionContext(context, includeInputs, includeOutputs, includeExecutionSockets));
    public void SetNodeExecuting(string nodeId, bool isExecuting) => Dispatch(() => State.SetNodeExecuting(nodeId, isExecuting));
    public void SetNodeError(string nodeId, bool isError) => Dispatch(() => State.SetNodeError(nodeId, isError));
    public void ResetNodeExecutionState() => Dispatch(() => State.ResetNodeExecutionState());

    // ── Node operations ─────────────────────────────────────────────────
    public NodeViewModel AddNode(NodeViewModel node) => Dispatch(() => State.AddNode(node));
    public void RemoveNode(string nodeId) => Dispatch(() => State.RemoveNode(nodeId));
    public void RemoveConnectionsToNode(string nodeId) => Dispatch(() => State.RemoveConnectionsToNode(nodeId));
    public void RemoveConnectionsToInput(string nodeId, string socketName) => Dispatch(() => State.RemoveConnectionsToInput(nodeId, socketName));
    public void RemoveConnectionsFromOutput(string nodeId, string socketName) => Dispatch(() => State.RemoveConnectionsFromOutput(nodeId, socketName));

    // ── Connection operations ───────────────────────────────────────────
    public void AddConnection(ConnectionData connection) => Dispatch(() => State.AddConnection(connection));
    public void RemoveConnection(ConnectionData connection) => Dispatch(() => State.RemoveConnection(connection));
    public void SelectConnection(ConnectionData connection, bool clearNodeSelection = true) => Dispatch(() => State.SelectConnection(connection, clearNodeSelection));
    public void ClearConnectionSelection() => Dispatch(() => State.ClearConnectionSelection());

    // ── Selection operations ────────────────────────────────────────────
    public void SelectNode(string nodeId, bool clearExisting = true) => Dispatch(() => State.SelectNode(nodeId, clearExisting));
    public void ToggleSelectNode(string nodeId) => Dispatch(() => State.ToggleSelectNode(nodeId));
    public void ClearSelection() => Dispatch(() => State.ClearSelection());
    public void SelectNodes(IEnumerable<string> nodeIds, bool clearExisting = true) => Dispatch(() => State.SelectNodes(nodeIds, clearExisting));
    public void SelectAll() => Dispatch(() => State.SelectAll());
    public void RemoveSelectedNodes() => Dispatch(() => State.RemoveSelectedNodes());

    // ── Overlay selection ───────────────────────────────────────────────
    public void SelectOverlay(string overlayId, bool clearExisting = true) => Dispatch(() => State.SelectOverlay(overlayId, clearExisting));
    public void ToggleSelectOverlay(string overlayId) => Dispatch(() => State.ToggleSelectOverlay(overlayId));
    public void ClearOverlaySelection() => Dispatch(() => State.ClearOverlaySelection());
    public void SelectOverlays(IEnumerable<string> overlayIds, bool clearExisting = true) => Dispatch(() => State.SelectOverlays(overlayIds, clearExisting));
    public void RemoveSelectedOverlays() => Dispatch(() => State.RemoveSelectedOverlays());

    // ── Overlay operations ──────────────────────────────────────────────
    public OverlayViewModel AddOverlay(OverlayViewModel overlay) => Dispatch(() => State.AddOverlay(overlay));
    public void RemoveOverlay(string overlayId) => Dispatch(() => State.RemoveOverlay(overlayId));

    // ── Undo / Redo ─────────────────────────────────────────────────────
    public void RequestUndo() => Dispatch(() => State.RequestUndo());
    public void RequestRedo() => Dispatch(() => State.RequestRedo());

    // ── Variables ───────────────────────────────────────────────────────
    public void AddVariable(GraphVariable variable) => Dispatch(() => State.AddVariable(variable));
    public void RemoveVariable(string variableId) => Dispatch(() => State.RemoveVariable(variableId));
    public void UpdateVariable(GraphVariable updated) => Dispatch(() => State.UpdateVariable(updated));
    public GraphVariable? FindVariable(string variableId) => State.FindVariable(variableId);

    // ── Events ──────────────────────────────────────────────────────────
    public void AddEvent(GraphEvent graphEvent) => Dispatch(() => State.AddEvent(graphEvent));
    public void RemoveEvent(string eventId) => Dispatch(() => State.RemoveEvent(eventId));
    public void UpdateEvent(GraphEvent updated) => Dispatch(() => State.UpdateEvent(updated));
    public GraphEvent? FindEvent(string eventId) => State.FindEvent(eventId);

    // ── Graph management ────────────────────────────────────────────────
    public GraphData ExportToGraphData() => State.ExportToGraphData();
    public void LoadFromGraphData(GraphData graphData) => Dispatch(() => State.LoadFromGraphData(graphData));
    public void Clear() => Dispatch(() => State.Clear());
}
