# NodeEditor.Blazor API Reference

Complete API reference for NodeEditor.Blazor library.

## Table of Contents

- [Core Services](#core-services)
  - [NodeEditorState](#nodeeditorstate)
  - [NodeExecutionService](#nodeexecutionservice)
  - [NodeRegistryService](#noderegistryservice)
  - [GraphSerializer](#graphserializer)
  - [CoordinateConverter](#coordinateconverter)
  - [ConnectionValidator](#connectionvalidator)
  - [SocketTypeResolver](#sockettyperesolver)
  - [ViewportCuller](#viewportculler)
- [Components](#components)
  - [NodeEditorCanvas](#nodeeditorcanvas)
  - [NodeComponent](#nodecomponent)
  - [SocketComponent](#socketcomponent)
  - [ConnectionPath](#connectionpath)
  - [ContextMenu](#contextmenu)
- [Models](#models)
  - [NodeData](#nodedata)
  - [SocketData](#socketdata)
    - [SocketEditorHint](#socketeditorhint)
    - [SocketEditorKind](#socketeditorkind)
    - [SocketEditorAttribute](#socketeditorattribute)
  - [ConnectionData](#connectiondata)
  - [GraphDto](#graphdto)
  - [Point2D, Size2D, Rect2D](#geometry-primitives)
- [View Models](#view-models)
  - [NodeViewModel](#nodeviewmodel)
  - [SocketViewModel](#socketviewmodel)
- [Interfaces](#interfaces)
  - [INodeContext](#inodecontext)
  - [INodePlugin](#inodeplugin)
  - [INodeCustomEditor](#inodecustomeditor)
- [Extension Points](#extension-points)

---

## Core Services

### NodeEditorState

Central state management class with event-based architecture.

**Namespace:** `NodeEditor.Blazor.Services`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Nodes` | `ObservableCollection<NodeViewModel>` | Collection of all nodes in the graph |
| `Connections` | `ObservableCollection<ConnectionData>` | Collection of all connections |
| `SelectedNodeIds` | `HashSet<string>` | Set of selected node IDs |
| `Zoom` | `double` | Current zoom level (1.0 = 100%) |
| `Viewport` | `Rect2D` | Current viewport rectangle |

#### Events

| Event | Args | Description |
|-------|------|-------------|
| `NodeAdded` | `NodeEventArgs` | Fired when a node is added |
| `NodeRemoved` | `NodeEventArgs` | Fired when a node is removed |
| `ConnectionAdded` | `ConnectionEventArgs` | Fired when a connection is added |
| `ConnectionRemoved` | `ConnectionEventArgs` | Fired when a connection is removed |
| `SelectionChanged` | `SelectionChangedEventArgs` | Fired when selection changes |
| `ViewportChanged` | `ViewportChangedEventArgs` | Fired when viewport changes |
| `ZoomChanged` | `ZoomChangedEventArgs` | Fired when zoom changes |
| `SocketValuesChanged` | `EventArgs` | Fired when socket values are updated |
| `UndoRequested` | `EventArgs` | Placeholder for undo functionality |
| `RedoRequested` | `EventArgs` | Placeholder for redo functionality |

#### Methods

**`void AddNode(NodeViewModel node)`**

Adds a node to the graph and raises the `NodeAdded` event.

```csharp
var node = new NodeViewModel(nodeData);
state.AddNode(node);
```

**`void RemoveNode(string nodeId)`**

Removes a node by ID, including all its connections, and raises events.

```csharp
state.RemoveNode("node-123");
```

**`void AddConnection(ConnectionData connection)`**

Adds a connection to the graph.

```csharp
var conn = new ConnectionData("node1", "node2", "Out", "In", false);
state.AddConnection(conn);
```

**`void RemoveConnection(ConnectionData connection)`**

Removes a connection from the graph.

**`void SelectNode(string nodeId, bool clearExisting = true)`**

Selects a node. If `clearExisting` is true, clears other selections.

**`void ToggleSelectNode(string nodeId)`**

Toggles the selection state of a node.

**`void SelectNodes(IEnumerable<string> nodeIds, bool clearExisting = true)`**

Selects multiple nodes.

**`void ClearSelection()`**

Clears all selections.

**`void SelectAll()`**

Selects all nodes.

**`void RemoveSelectedNodes()`**

Removes all currently selected nodes.

**`void Clear()`**

Clears all nodes and connections, resets viewport and zoom.

**`IReadOnlyList<NodeData> BuildExecutionNodes()`**

Builds a snapshot of node data using current socket values from view models.

**`void ApplyExecutionContext(INodeExecutionContext context, bool includeInputs = true, bool includeOutputs = true, bool includeExecutionSockets = false)`**

Updates socket values from an execution context and raises `SocketValuesChanged`.

---

### NodeExecutionService

Executes node graphs with dependency resolution and parallel/sequential modes.

**Namespace:** `NodeEditor.Blazor.Services.Execution`

#### Events

| Event | Args | Description |
|-------|------|-------------|
| `NodeStarted` | `NodeExecutionEventArgs` | Fired when a node begins execution |
| `NodeCompleted` | `NodeExecutionEventArgs` | Fired when a node completes |
| `ExecutionFailed` | `ExecutionFailedEventArgs` | Fired when execution fails |
| `ExecutionCanceled` | `EventArgs` | Fired when execution is canceled |
| `LayerStarted` | `LayerEventArgs` | Fired when an execution layer begins |
| `LayerCompleted` | `LayerEventArgs` | Fired when a layer completes |

#### Methods

**`Task ExecuteAsync(IReadOnlyList<NodeData> nodes, IReadOnlyList<ConnectionData> connections, INodeExecutionContext context, INodeContext nodeContext, NodeExecutionOptions options, CancellationToken token)`**

Executes a node graph.

**Parameters:**
- `nodes` - List of nodes to execute
- `connections` - List of connections between nodes
- `context` - Execution context for storing socket values
- `nodeContext` - Context providing node method implementations
- `options` - Execution configuration options
- `token` - Cancellation token

**Example:**

```csharp
var service = serviceProvider.GetRequiredService<NodeExecutionService>();
var context = new NodeExecutionContext();
var nodeContext = new StandardNodeContext();

var options = new NodeExecutionOptions
{
    Mode = ExecutionMode.Parallel,
    MaxDegreeOfParallelism = 4
};

await service.ExecuteAsync(
    nodes: state.BuildExecutionNodes(),
    connections: state.Connections.ToList(),
    context: context,
    nodeContext: nodeContext,
    options: options,
    token: cancellationToken
);
```

---

### NodeRegistryService

Manages available node definitions for the context menu and node creation.

**Namespace:** `NodeEditor.Blazor.Services.Registry`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Definitions` | `IReadOnlyList<NodeDefinition>` | All registered node definitions |

#### Events

| Event | Args | Description |
|-------|------|-------------|
| `RegistryChanged` | `EventArgs` | Fired when the registry is updated |

#### Methods

**`void EnsureInitialized(IEnumerable<Assembly>? assemblies = null)`**

Initializes the registry by discovering nodes from assemblies.

**`void RegisterFromAssembly(Assembly assembly)`**

Registers all nodes from a given assembly.

**`void RegisterDefinitions(IEnumerable<NodeDefinition> definitions)`**

Registers a collection of node definitions.

**`NodeCatalog GetCatalog(string? search = null)`**

Returns a catalog of nodes grouped by category, optionally filtered by search query.

**Example:**

```csharp
var catalog = registry.GetCatalog("math");
foreach (var category in catalog.Categories)
{
    Console.WriteLine($"Category: {category.Name}");
    foreach (var def in category.Definitions)
    {
        Console.WriteLine($"  - {def.Name}");
    }
}
```

---

### GraphSerializer

Serializes and deserializes node graphs to/from JSON.

**Namespace:** `NodeEditor.Blazor.Services.Serialization`

#### Methods

**`GraphDto Export(NodeEditorState state)`**

Exports the current state to a DTO.

**`string Serialize(GraphDto dto)`**

Serializes a DTO to JSON string.

**`GraphDto Deserialize(string json)`**

Deserializes JSON string to a DTO.

**`GraphImportResult Import(NodeEditorState state, GraphDto dto)`**

Imports a DTO into the state. Returns a result with warnings if any.

**Example:**

```csharp
// Save
var dto = serializer.Export(state);
var json = serializer.Serialize(dto);
File.WriteAllText("graph.json", json);

// Load
var json = File.ReadAllText("graph.json");
var dto = serializer.Deserialize(json);
var result = serializer.Import(state, dto);

foreach (var warning in result.Warnings)
{
    Console.WriteLine($"Warning: {warning}");
}
```

---

### CoordinateConverter

Converts between screen coordinates and graph coordinates for pan/zoom operations.

**Namespace:** `NodeEditor.Blazor.Services`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `PanOffset` | `Point2D` | Current pan offset in screen pixels |
| `Zoom` | `double` | Current zoom level |

#### Methods

**`Point2D ScreenToGraph(Point2D screenPoint)`**

Converts screen coordinates to graph coordinates.

**`Point2D GraphToScreen(Point2D graphPoint)`**

Converts graph coordinates to screen coordinates.

**`Rect2D ScreenToGraph(Rect2D screenRect)`**

Converts a screen rectangle to graph coordinates.

**`Rect2D GraphToScreen(Rect2D graphRect)`**

Converts a graph rectangle to screen coordinates.

**`Point2D ScreenDeltaToGraph(Point2D screenDelta)`**

Converts a screen delta (movement) to graph delta.

**`void SyncFromState(NodeEditorState state)`**

Updates pan and zoom from state.

**`Point2D ComputeZoomCenteredPan(Point2D focusScreenPoint, double oldZoom, double newZoom)`**

Computes the new pan offset for zoom centered on a specific screen point.

---

### ConnectionValidator

Validates whether two sockets can be connected.

**Namespace:** `NodeEditor.Blazor.Services`

#### Methods

**`bool CanConnect(SocketData source, SocketData target)`**

Returns true if the source and target sockets can be connected.

**Validation Rules:**
- Source must be an output, target must be an input
- Both must be execution sockets or both must be data sockets
- Data socket types must be compatible

---

### SocketTypeResolver

Resolves socket types for method invocation.

**Namespace:** `NodeEditor.Blazor.Services`

#### Methods

**`void Register(string typeName, Type type)`**

Registers a type name to a .NET type mapping.

**`Type? Resolve(string typeName)`**

Resolves a type name to a .NET type. Returns null if not found.

---

### ViewportCuller

Performance optimization service that computes which nodes and connections are visible in the current viewport.

**Namespace:** `NodeEditor.Blazor.Services`

#### Methods

**`IReadOnlyList<NodeViewModel> GetVisibleNodes(IReadOnlyList<NodeViewModel> nodes, Rect2D screenViewport, IEnumerable<string>? alwaysIncludeNodeIds = null)`**

Returns only the nodes that intersect the viewport, plus any explicitly included nodes (e.g., currently being dragged).

**`IReadOnlyList<ConnectionData> GetVisibleConnections(IReadOnlyList<ConnectionData> connections, IReadOnlyCollection<NodeViewModel> visibleNodes, IEnumerable<string>? alwaysIncludeNodeIds = null)`**

Returns connections that have at least one endpoint in the visible nodes set.

---

## Components

### NodeEditorCanvas

Main canvas component for rendering and interacting with the node graph.

**Namespace:** `NodeEditor.Blazor.Components`

#### Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `State` | `NodeEditorState` | Yes | - | The state instance to bind to |
| `MinZoom` | `double` | No | 0.1 | Minimum zoom level |
| `MaxZoom` | `double` | No | 3.0 | Maximum zoom level |
| `ZoomStep` | `double` | No | 0.1 | Zoom increment per scroll |

#### Usage

```razor
@inject NodeEditorState EditorState

<NodeEditorCanvas State="@EditorState" 
                  MinZoom="0.2" 
                  MaxZoom="5.0" 
                  ZoomStep="0.15" />
```

#### Interactions

| Action | Description |
|--------|-------------|
| Left-click + drag on canvas | Box selection |
| Left-click + drag on node | Drag node (or selected nodes) |
| Left-click + drag from socket | Create connection |
| Middle-click + drag | Pan canvas |
| Mouse wheel | Zoom in/out |
| Right-click | Open context menu |
| Delete / Backspace | Delete selected nodes |
| Ctrl+A | Select all |
| Ctrl+Z | Request undo (placeholder) |
| Ctrl+Y | Request redo (placeholder) |
| Escape | Cancel operation |

---

### NodeComponent

Renders an individual node with sockets.

**Namespace:** `NodeEditor.Blazor.Components`

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Node` | `NodeViewModel` | Yes | The node to render |
| `Connections` | `IReadOnlyList<ConnectionData>` | No | Connection list for determining socket state |
| `OnSocketPointerDown` | `EventCallback<SocketPointerEventArgs>` | No | Called when socket pointer down |
| `OnSocketPointerUp` | `EventCallback<SocketPointerEventArgs>` | No | Called when socket pointer up |
| `OnNodeDragStart` | `EventCallback<NodePointerEventArgs>` | No | Called when node drag starts |

---

### SocketComponent

Renders a socket (input or output) on a node.

**Namespace:** `NodeEditor.Blazor.Components`

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Socket` | `SocketViewModel` | Yes | The socket to render |
| `NodeId` | `string` | Yes | Parent node ID |
| `Node` | `NodeViewModel` | Yes | Parent node view model |
| `IsConnected` | `bool` | No | Whether the socket is connected |
| `OnPointerDown` | `EventCallback<SocketPointerEventArgs>` | No | Pointer down callback |
| `OnPointerUp` | `EventCallback<SocketPointerEventArgs>` | No | Pointer up callback |

---

### ConnectionPath

Renders an SVG path for a connection between nodes.

**Namespace:** `NodeEditor.Blazor.Components`

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `Connection` | `ConnectionData` | Yes | The connection to render |
| `Nodes` | `ObservableCollection<NodeViewModel>` | Yes | Node collection for endpoint lookup |
| `IsPending` | `bool` | No | Whether this is a pending connection |
| `PendingEndPoint` | `Point2D?` | No | End point for pending connection |

---

### ContextMenu

Right-click context menu for adding nodes.

**Namespace:** `NodeEditor.Blazor.Components`

#### Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `IsOpen` | `bool` | Yes | Whether the menu is open |
| `Position` | `Point2D` | Yes | Menu position in screen coordinates |
| `OnSelect` | `EventCallback<NodeDefinition>` | Yes | Called when a node is selected |
| `OnClose` | `EventCallback` | Yes | Called when the menu closes |

---

## Models

### NodeData

Immutable data model for a node.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public sealed record class NodeData(
    string Id,
    string Name,
    bool Callable,
    bool ExecInit,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    string? DefinitionId = null
);
```

---

### SocketData

Immutable data model for a socket.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public sealed record class SocketData(
    string Name,
    string TypeName,
    bool IsInput,
    bool IsExecution,
    SocketValue? Value = null,
    SocketEditorHint? EditorHint = null
);
```

---

### SocketEditorHint

Optional editor metadata for a socket, provided via `[SocketEditor]` on node parameters.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public sealed record class SocketEditorHint(
    SocketEditorKind Kind,
    string? Options = null,
    double? Min = null,
    double? Max = null,
    double? Step = null,
    string? Placeholder = null,
    string? Label = null);
```

---

### SocketEditorKind

Editor types supported by the standard UI layer.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public enum SocketEditorKind
{
    Text,
    Number,
    Bool,
    Dropdown,
    Button,
    Image,
    NumberUpDown,
    TextArea,
    Custom
}
```

---

### SocketEditorAttribute

Attribute for selecting a standard editor on node input parameters.

**Namespace:** `NodeEditor.Blazor.Services.Execution`

```csharp
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false)]
public sealed class SocketEditorAttribute : Attribute
{
    public SocketEditorAttribute(SocketEditorKind kind);

    public SocketEditorKind Kind { get; }
    public string? Options { get; init; }
    public double Min { get; init; }
    public double Max { get; init; }
    public double Step { get; init; }
    public string? Placeholder { get; init; }
    public string? Label { get; init; }
}
```

---

### ConnectionData

Immutable data model for a connection.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public sealed record class ConnectionData(
    string OutputNodeId,
    string InputNodeId,
    string OutputSocketName,
    string InputSocketName,
    bool IsExecution
);
```

---

### GraphDto

Data transfer object for graph serialization.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public sealed record class GraphDto(
    int Version,
    IReadOnlyList<NodeDto> Nodes,
    IReadOnlyList<ConnectionDto> Connections,
    ViewportDto Viewport,
    IReadOnlyList<string> SelectedNodeIds
);
```

---

### Geometry Primitives

**Point2D**

```csharp
public readonly record struct Point2D(double X, double Y);
```

**Size2D**

```csharp
public readonly record struct Size2D(double Width, double Height);
```

**Rect2D**

```csharp
public readonly record struct Rect2D(double X, double Y, double Width, double Height)
{
    public Point2D Location { get; }
    public Size2D Size { get; }
    public bool Intersects(Rect2D other);
    public bool Contains(Point2D point);
    public static Rect2D FromLocationSize(Point2D location, Size2D size);
}
```

---

## View Models

### NodeViewModel

MVVM view model for a node with observable properties.

**Namespace:** `NodeEditor.Blazor.ViewModels`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Data` | `NodeData` | Immutable node data |
| `Inputs` | `IReadOnlyList<SocketViewModel>` | Input socket view models |
| `Outputs` | `IReadOnlyList<SocketViewModel>` | Output socket view models |
| `Position` | `Point2D` | Node position (observable) |
| `Size` | `Size2D` | Node size (observable) |
| `IsSelected` | `bool` | Selection state (observable) |

---

### SocketViewModel

MVVM view model for a socket.

**Namespace:** `NodeEditor.Blazor.ViewModels`

#### Properties

| Property | Type | Description |
|----------|------|-------------|
| `Data` | `SocketData` | Socket data (updates on SetValue) |

#### Methods

**`void SetValue(object? value)`**

Sets the socket value and updates the Data property.

---

## Interfaces

### INodeContext

Interface for providing node method implementations.

**Namespace:** `NodeEditor.Blazor.Models`

```csharp
public interface INodeContext
{
    event EventHandler<FeedbackEventArgs>? FeedbackInfo;
    event EventHandler<FeedbackEventArgs>? FeedbackWarning;
    event EventHandler<FeedbackEventArgs>? FeedbackError;
}
```

Methods with `[Node]` attribute will be discovered and registered.

---

### INodePlugin

Interface for creating node plugins.

**Namespace:** `NodeEditor.Blazor.Services.Plugins`

```csharp
public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }
    
    void Register(NodeRegistryService registry);
}
```

---

### INodeCustomEditor

Interface for creating custom socket value editors.

**Namespace:** `NodeEditor.Blazor.Services.Editors`

```csharp
public interface INodeCustomEditor
{
    bool CanEdit(SocketData socket);
    RenderFragment Render(SocketEditorContext context);
}
```

---

## Extension Points

### Custom Editors

Register custom editors in DI:

```csharp
services.AddSingleton<INodeCustomEditor, ColorEditorDefinition>();
```

### Plugins

Create assemblies implementing `INodePlugin` and load them:

```csharp
await pluginLoader.LoadAndRegisterAsync("./plugins");
```

### Node Contexts

Create classes implementing `INodeContext` with `[Node]` attributed methods:

```csharp
public class MyContext : INodeContext
{
    [Node("My Node", category: "Custom")]
    public void MyNode(int Input, out int Output)
    {
        Output = Input * 2;
    }
}
```

Register in DI and pass to execution service.

---

## Performance Considerations

### Viewport Culling

The `ViewportCuller` service automatically filters visible nodes/connections in `NodeEditorCanvas`. For large graphs (500+ nodes), this provides significant performance improvements.

### ShouldRender Optimization

`NodeComponent` and `ConnectionPath` implement `ShouldRender` to prevent unnecessary re-renders when their properties haven't changed.

### Event Subscriptions

Always dispose event subscriptions in Blazor components:

```csharp
public void Dispose()
{
    State.NodeAdded -= OnNodeAdded;
    State.SelectionChanged -= OnSelectionChanged;
}
```

---

## Version History

- **v1.0.0** - Initial release with full node editor functionality
