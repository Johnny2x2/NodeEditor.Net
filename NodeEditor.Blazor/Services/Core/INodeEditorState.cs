using System.Collections.ObjectModel;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Abstraction for the editor's central state store.
/// </summary>
public interface INodeEditorState
{
    // Events
    event EventHandler<NodeEventArgs>? NodeAdded;
    event EventHandler<NodeEventArgs>? NodeRemoved;
    event EventHandler<ConnectionEventArgs>? ConnectionAdded;
    event EventHandler<ConnectionEventArgs>? ConnectionRemoved;
    event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    event EventHandler<ConnectionSelectionChangedEventArgs>? ConnectionSelectionChanged;
    event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
    event EventHandler<ZoomChangedEventArgs>? ZoomChanged;
    event EventHandler? SocketValuesChanged;
    event EventHandler<NodeEventArgs>? NodeExecutionStateChanged;
    event EventHandler? UndoRequested;
    event EventHandler? RedoRequested;
    event EventHandler<GraphVariableEventArgs>? VariableAdded;
    event EventHandler<GraphVariableEventArgs>? VariableRemoved;
    event EventHandler<GraphVariableChangedEventArgs>? VariableChanged;
    event EventHandler<GraphEventEventArgs>? EventAdded;
    event EventHandler<GraphEventEventArgs>? EventRemoved;
    event EventHandler<GraphEventChangedEventArgs>? EventChanged;

    // Collections
    ObservableCollection<NodeViewModel> Nodes { get; }
    ObservableCollection<ConnectionData> Connections { get; }
    ObservableCollection<GraphVariable> Variables { get; }
    ObservableCollection<GraphEvent> Events { get; }
    HashSet<string> SelectedNodeIds { get; }
    ConnectionData? SelectedConnection { get; }

    // Viewport
    double Zoom { get; set; }
    Rect2D Viewport { get; set; }

    // Execution bridge
    IReadOnlyList<NodeData> BuildExecutionNodes();
    void ApplyExecutionContext(INodeExecutionContext context, bool includeInputs = true, bool includeOutputs = true, bool includeExecutionSockets = false);
    void SetNodeExecuting(string nodeId, bool isExecuting);
    void SetNodeError(string nodeId, bool isError);
    void ResetNodeExecutionState();

    // Node operations
    NodeViewModel AddNode(NodeViewModel node);
    void RemoveNode(string nodeId);
    void RemoveConnectionsToNode(string nodeId);
    void RemoveConnectionsToInput(string nodeId, string socketName);
    void RemoveConnectionsFromOutput(string nodeId, string socketName);

    // Connection operations
    void AddConnection(ConnectionData connection);
    void RemoveConnection(ConnectionData connection);
    void SelectConnection(ConnectionData connection, bool clearNodeSelection = true);
    void ClearConnectionSelection();

    // Selection operations
    void SelectNode(string nodeId, bool clearExisting = true);
    void ToggleSelectNode(string nodeId);
    void ClearSelection();
    void SelectNodes(IEnumerable<string> nodeIds, bool clearExisting = true);
    void SelectAll();
    void RemoveSelectedNodes();

    // Undo/redo
    void RequestUndo();
    void RequestRedo();

    // Variables
    void AddVariable(GraphVariable variable);
    void RemoveVariable(string variableId);
    void UpdateVariable(GraphVariable updated);
    GraphVariable? FindVariable(string variableId);

    // Events
    void AddEvent(GraphEvent graphEvent);
    void RemoveEvent(string eventId);
    void UpdateEvent(GraphEvent updated);
    GraphEvent? FindEvent(string eventId);

    // Graph management
    GraphData ExportToGraphData();
    void LoadFromGraphData(GraphData graphData);
    void Clear();
}
