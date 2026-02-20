# NodeEditor.Blazor Wiring & Event Flow Guide

## Overview

This document provides detailed insight into how NodeEditor.Blazor components are wired together, how events flow through the system, and how the reactive architecture responds to user interactions.

---

## Table of Contents

1. [Dependency Injection Wiring](#dependency-injection-wiring)
2. [Component Event Propagation](#component-event-propagation)
3. [State Change Propagation](#state-change-propagation)
4. [Connection Creation Flow](#connection-creation-flow)
5. [Node Execution Flow](#node-execution-flow)
6. [Plugin Loading & Registration Flow](#plugin-loading--registration-flow)
7. [Plugin Lifecycle Hooks](#plugin-lifecycle-hooks)
8. [Plugin Event Bus](#plugin-event-bus)
9. [Serialization & Deserialization Flow](#serialization--deserialization-flow)
10. [Viewport & Coordinate Transformation](#viewport--coordinate-transformation)

---

## Dependency Injection Wiring

### Service Registration Graph

```mermaid
graph TB
    subgraph "Application Startup"
        Main[Program.cs/Startup.cs]
        AddNodeEditor[services.AddNodeEditor]
    end
    
    subgraph "Singleton Services Created Once"
        PL[PluginLoader]
        PSR[IPluginServiceRegistry]
        NRS[INodeRegistryService]
        NDS[NodeDiscoveryService]
        STR[SocketTypeResolver]
        EP[ExecutionPlanner]
        GSM[GraphSchemaMigrator]
        BEQ[BackgroundExecutionQueue]
    end
    
    subgraph "Scoped Services Per User/Circuit"
        NES[NodeEditorState]
        PEB[IPluginEventBus]
        CC[CoordinateConverter]
        CV[ConnectionValidator]
        TGH[TouchGestureHandler]
        VC[ViewportCuller]
        NS[NodeExecutionService]
        GS[GraphSerializer]
        BEW[BackgroundExecutionWorker]
    end
    
    subgraph "Components"
        Canvas[NodeEditorCanvas]
        NodeComp[NodeComponent]
        PluginMgr[PluginManager]
    end
    
    Main --> AddNodeEditor
    AddNodeEditor --> PL
    AddNodeEditor --> PSR
    AddNodeEditor --> NRS
    AddNodeEditor --> NDS
    AddNodeEditor --> STR
    AddNodeEditor --> EP
    AddNodeEditor --> GSM
    AddNodeEditor --> BEQ
    
    AddNodeEditor --> NES
    AddNodeEditor --> PEB
    AddNodeEditor --> CC
    AddNodeEditor --> CV
    AddNodeEditor --> TGH
    AddNodeEditor --> VC
    AddNodeEditor --> NS
    AddNodeEditor --> GS
    AddNodeEditor --> BEW
    
    NDS --> NRS
    NRS --> GS
    CV --> Canvas
    CC --> Canvas
    VC --> Canvas
    TGH --> Canvas
    NES --> Canvas
    NES --> NodeComp
    PEB --> Canvas
    PSR --> PL
    
    style AddNodeEditor fill:#4a90e2,color:#fff
    style NES fill:#50c878,color:#fff
    style PEB fill:#ff6b6b,color:#fff
    style PSR fill:#ff6b6b,color:#fff
```

### Injection into Components

Each Blazor component declares dependencies at the top:

```razor
@inject NodeEditorState State
@inject CoordinateConverter CoordinateConverter
@inject ConnectionValidator ConnectionValidator
@inject IJSRuntime JSRuntime
```

Plugin-aware components and services can also inject:

```razor
@inject IPluginEventBus PluginEventBus
```

**Wiring Flow:**

1. **Application Start**: `services.AddNodeEditor()` is called
2. **Service Registration**: All services are registered with appropriate lifetimes
3. **Component Creation**: When Blazor instantiates a component, it injects dependencies
4. **OnInitialized**: Components subscribe to service events
5. **OnDispose**: Components unsubscribe to prevent memory leaks

```mermaid
sequenceDiagram
    participant Blazor as Blazor Runtime
    participant DI as Dependency Injector
    participant Component as NodeEditorCanvas
    participant State as NodeEditorState
    
    Blazor->>DI: Create NodeEditorCanvas
    DI->>DI: Resolve dependencies
    DI->>State: Get/Create scoped instance
    DI->>Component: Inject State, CoordinateConverter, etc.
    Component->>Component: OnInitialized()
    Component->>State: Subscribe to events
    Note over Component,State: Component now reactive to state changes
```

---

## Component Event Propagation

### User Interaction to State Update

```mermaid
graph LR
    subgraph "DOM Events"
        MouseDown[onpointerdown]
        MouseMove[onpointermove]
        MouseUp[onpointerup]
        KeyDown[onkeydown]
        Wheel[onwheel]
    end
    
    subgraph "Canvas Event Handlers"
        OnPointerDown[OnPointerDown]
        OnPointerMove[OnPointerMove]
        OnPointerUp[OnPointerUp]
        OnKeyDown[OnKeyDown]
        OnWheel[OnWheel]
    end
    
    subgraph "State Mutations"
        AddNode[AddNode]
        AddConn[AddConnection]
        SelectNode[SelectNode]
        UpdateViewport[Viewport = newValue]
    end
    
    subgraph "Event Publishing"
        NodeAdded[NodeAdded Event]
        ConnAdded[ConnectionAdded Event]
        SelectionChanged[SelectionChanged Event]
        ViewportChanged[ViewportChanged Event]
    end
    
    MouseDown --> OnPointerDown
    MouseMove --> OnPointerMove
    MouseUp --> OnPointerUp
    KeyDown --> OnKeyDown
    Wheel --> OnWheel
    
    OnPointerDown --> SelectNode
    OnPointerMove --> UpdateViewport
    OnPointerUp --> AddConn
    OnKeyDown --> AddNode
    
    SelectNode --> SelectionChanged
    AddConn --> ConnAdded
    AddNode --> NodeAdded
    UpdateViewport --> ViewportChanged
    
    style OnPointerDown fill:#4a90e2,color:#fff
    style SelectNode fill:#ff6b6b,color:#fff
    style SelectionChanged fill:#50c878,color:#fff
```

### Component-to-Component Communication

Components **never communicate directly**. All communication flows through **NodeEditorState**:

```mermaid
graph TB
    subgraph "Parent Component"
        Canvas[NodeEditorCanvas]
    end
    
    subgraph "Child Components"
        Node1[NodeComponent A]
        Node2[NodeComponent B]
        Conn[ConnectionPath]
    end
    
    subgraph "Shared State"
        State[NodeEditorState]
    end
    
    Node1 -->|User drags socket| Canvas
    Canvas -->|AddConnection| State
    State -->|ConnectionAdded Event| Canvas
    State -->|ConnectionAdded Event| Node1
    State -->|ConnectionAdded Event| Node2
    State -->|ConnectionAdded Event| Conn
    
    Canvas -.->|StateHasChanged| Canvas
    Node1 -.->|StateHasChanged| Node1
    Node2 -.->|StateHasChanged| Node2
    
    style State fill:#4a90e2,color:#fff
```

### Event Subscription Pattern

```csharp
// In NodeEditorCanvas.razor.cs
protected override void OnInitialized()
{
    // Subscribe to all relevant state events
    State.NodeAdded += OnNodeAdded;
    State.NodeRemoved += OnNodeRemoved;
    State.ConnectionAdded += OnConnectionAdded;
    State.ConnectionRemoved += OnConnectionRemoved;
    State.ViewportChanged += OnViewportChanged;
    State.ZoomChanged += OnZoomChanged;
    State.SelectionChanged += OnSelectionChanged;
}

private void OnNodeAdded(object? sender, StateChangeEventArgs<NodeViewModel> e)
{
    // Update culling, refresh visible nodes
    UpdateVisibleNodes();
    StateHasChanged(); // Trigger Blazor re-render
}

public void Dispose()
{
    // CRITICAL: Unsubscribe to prevent memory leaks
    State.NodeAdded -= OnNodeAdded;
    State.NodeRemoved -= OnNodeRemoved;
    // ... unsubscribe all
}
```

---

## State Change Propagation

### How State Changes Trigger UI Updates

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant State as NodeEditorState
    participant Blazor as Blazor Renderer
    participant NodeComp as NodeComponent
    
    User->>Canvas: Clicks "Add Node"
    Canvas->>State: AddNode(nodeViewModel)
    State->>State: Nodes.Add(node)
    
    Note over State: Raise event
    State->>Canvas: NodeAdded Event
    State->>NodeComp: NodeAdded Event
    
    Canvas->>Canvas: UpdateVisibleNodes()
    Canvas->>Blazor: StateHasChanged()
    
    NodeComp->>Blazor: StateHasChanged()
    
    Blazor->>Blazor: Compute diff
    Blazor->>Canvas: Re-render affected markup
    Blazor->>NodeComp: Render new node
    
    Canvas->>User: UI updated
```

### Event Cascading

When one state change triggers multiple reactions:

```mermaid
graph TB
    StateChange[State.AddConnection]
    
    subgraph "Immediate Events"
        ConnAdded[ConnectionAdded]
    end
    
    subgraph "Subscribers React"
        Canvas[Canvas: UpdateConnections]
        NodeA[NodeA: UpdateSockets]
        NodeB[NodeB: UpdateSockets]
        Serializer[Serializer: MarkDirty]
    end
    
    subgraph "Side Effects"
        Render1[Canvas.StateHasChanged]
        Render2[NodeA.StateHasChanged]
        Render3[NodeB.StateHasChanged]
    end
    
    subgraph "Blazor Render"
        Diff[Compute Render Tree Diff]
        Update[Update DOM]
    end
    
    StateChange --> ConnAdded
    ConnAdded --> Canvas
    ConnAdded --> NodeA
    ConnAdded --> NodeB
    ConnAdded --> Serializer
    
    Canvas --> Render1
    NodeA --> Render2
    NodeB --> Render3
    
    Render1 --> Diff
    Render2 --> Diff
    Render3 --> Diff
    
    Diff --> Update
    
    style ConnAdded fill:#4a90e2,color:#fff
    style Diff fill:#ff6b6b,color:#fff
```

---

## Connection Creation Flow

### Complete Connection Creation Sequence

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant State as NodeEditorState
    participant Validator as ConnectionValidator
    participant SVG as SVG Layer
    
    User->>Canvas: Pointer down on output socket
    Canvas->>Canvas: Store _dragStartSocket
    Canvas->>Canvas: Create _pendingConnection
    Canvas->>SVG: Render pending connection
    
    User->>Canvas: Pointer move
    loop While dragging
        Canvas->>Canvas: Update _pendingConnectionEndGraph
        Canvas->>SVG: Update pending connection path
    end
    
    User->>Canvas: Pointer up on input socket
    Canvas->>Canvas: Store _dragEndSocket
    
    Canvas->>Validator: CanConnect(output, input)
    
    alt Connection Valid
        Validator-->>Canvas: true
        Canvas->>Canvas: CreateConnectionData()
        Canvas->>State: AddConnection(connectionData)
        State->>State: Connections.Add(connection)
        State->>Canvas: Raise ConnectionAdded
        Canvas->>Canvas: Clear _pendingConnection
        Canvas->>Canvas: StateHasChanged()
        Canvas->>SVG: Render permanent connection
    else Connection Invalid
        Validator-->>Canvas: false
        Canvas->>Canvas: Clear _pendingConnection
        Canvas->>Canvas: Show error (visual feedback)
        Canvas->>Canvas: StateHasChanged()
    end
```

### Validation Rules Applied

```mermaid
graph TB
    Start[User drops connection]
    
    Check1{Source != Target Node?}
    Check2{Output to Input?}
    Check3{Types Compatible?}
    Check4{Connection Exists?}
    Check5{Input allows connection?}
    
    Accept[Create Connection]
    Reject[Reject & Show Error]
    
    Start --> Check1
    Check1 -->|No| Reject
    Check1 -->|Yes| Check2
    Check2 -->|No| Reject
    Check2 -->|Yes| Check3
    Check3 -->|No| Reject
    Check3 -->|Yes| Check4
    Check4 -->|Yes| Reject
    Check4 -->|No| Check5
    Check5 -->|No| Reject
    Check5 -->|Yes| Accept
    
    style Accept fill:#50c878,color:#fff
    style Reject fill:#ff6b6b,color:#fff
```

**Type Compatibility:**

```csharp
SocketTypeResolver checks:
1. Exact type match: int → int ✓
2. Assignable types: IEnumerable<T> → List<T> ✓
3. Base/derived: object → string ✓
4. Incompatible: int → string ✗
```

---

## Node Execution Flow

### End-to-End Execution Sequence

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant State as NodeEditorState
    participant ExecService as NodeExecutionService
    participant Planner as ExecutionPlanner
    participant Invoker as NodeMethodInvoker
    participant Context as NodeBase
    
    User->>Canvas: Clicks "Execute" on node
    Canvas->>ExecService: ExecuteAsync(nodeId, Sequential)
    
    ExecService->>State: BuildExecutionNodes()
    State-->>ExecService: ExecutionNodeData[]
    
    ExecService->>Planner: BuildPlan(nodes, connections)
    Planner->>Planner: Topological sort
    Planner->>Planner: Assign layers
    Planner-->>ExecService: ExecutionPlan
    
    ExecService->>ExecService: Raise ExecutionStarted
    
    loop For each layer
        ExecService->>ExecService: Raise LayerStarted
        
        par Parallel node execution
            ExecService->>State: SetNodeExecuting(nodeId)
            State->>Canvas: NodeExecutionStateChanged
            Canvas->>Canvas: Update node visual (pulsing border)
            
            ExecService->>ExecService: ResolveInputsAsync(node)
            Note over ExecService: Get values from connected outputs
            
            ExecService->>Invoker: InvokeAsync(definition, inputs)
            Invoker->>Context: MethodInfo.Invoke(context, parameters)
            Context->>Context: Execute business logic
            Context-->>Invoker: Return value
            Invoker-->>ExecService: NodeExecutionContext
            
            alt Execution Success
                ExecService->>State: ApplyExecutionContext(result)
                State->>State: Update output socket values
                State->>Canvas: SocketValuesChanged
            else Execution Failed
                ExecService->>State: SetNodeError(nodeId, errorMsg)
                State->>Canvas: NodeExecutionStateChanged
                Canvas->>Canvas: Show error state (red border)
            end
        end
        
        ExecService->>ExecService: Raise LayerCompleted
    end
    
    ExecService->>ExecService: Raise ExecutionCompleted
    ExecService-->>Canvas: Task completes
    Canvas->>Canvas: Final StateHasChanged()
```

### Input Resolution Strategy

```mermaid
graph TB
    StartResolve[Resolve Input for Socket]
    
    HasConnection{Has Incoming Connection?}
    GetConnected[Get Connected Output Socket]
    GetValue[Get Socket.Value]
    GetDefault[Use Parameter Default]
    
    TypeConvert{Type Conversion Needed?}
    Convert[Apply Type Conversion]
    PassThrough[Use Value As-Is]
    
    Return[Return Resolved Value]
    
    StartResolve --> HasConnection
    HasConnection -->|Yes| GetConnected
    HasConnection -->|No| GetValue
    
    GetConnected --> TypeConvert
    GetValue --> TypeConvert
    GetValue -.fallback.-> GetDefault
    
    TypeConvert -->|Yes| Convert
    TypeConvert -->|No| PassThrough
    
    Convert --> Return
    PassThrough --> Return
    GetDefault --> Return
    
    style Return fill:#50c878,color:#fff
```

### Execution State Visualization

The UI reflects execution state in real-time:

| State | Visual Indicator | CSS Class |
|-------|-----------------|-----------|
| **Idle** | Normal appearance | `.ne-node` |
| **Executing** | Pulsing blue border | `.ne-node.executing` |
| **Success** | Brief green flash | `.ne-node.success` |
| **Error** | Red border + error icon | `.ne-node.error` |

```css
.ne-node.executing {
    animation: pulse 1.5s infinite;
    border-color: #4a90e2;
}

.ne-node.error {
    border-color: #ff6b6b;
    box-shadow: 0 0 10px rgba(255, 107, 107, 0.5);
}
```

---

## Plugin Loading & Registration Flow

### Complete Plugin Lifecycle

```mermaid
sequenceDiagram
    participant App as Application Startup
    participant Loader as PluginLoader
    participant PSR as IPluginServiceRegistry
    participant LoadCtx as PluginLoadContext
    participant Assembly as Plugin Assembly
    participant Registry as INodeRegistryService
    participant UI as UI Components
    
    App->>Loader: LoadAndRegisterAsync(services)
    Loader->>Loader: Discover plugin directories
    
    loop For each plugin
        Loader->>Loader: Read manifest.json
        Loader->>LoadCtx: Create isolated context
        LoadCtx->>Assembly: Load plugin DLL
        Assembly-->>Loader: Assembly loaded
        
        Loader->>Loader: Validate(plugin, manifest)
        
        alt Valid Plugin
            Loader->>Loader: OnLoadAsync()
            Loader->>PSR: RegisterServices(pluginId, ConfigureServices)
            Loader->>Registry: Register(registry)
            Loader->>Loader: Register definitions (INodeProvider)
            Loader->>Loader: OnInitializeAsync(services)
            Registry->>UI: Raise RegistryChanged Event
            UI->>UI: Refresh context menu
            
            Loader->>Loader: Track in _loadedPlugins
            Loader-->>App: Plugin loaded successfully
        else Invalid Plugin
            Loader->>Loader: Log error
            Loader->>Loader: OnError(exception)
            Loader-->>App: Skip plugin
        end
    end
```

### Plugin Manifest Structure

```json
{
  "pluginId": "com.example.myplugin",
  "name": "My Custom Plugin",
  "version": "1.0.0",
  "author": "Your Name",
  "description": "Adds custom nodes for data processing",
  "mainAssembly": "MyPlugin.dll",
  "dependencies": [
    {
      "name": "Newtonsoft.Json",
      "version": "13.0.0"
    }
  ]
}
```

### Registry Update Flow

```mermaid
graph LR
    PluginAssembly[Plugin Assembly]
    Discovery[NodeDiscoveryService]
    Definitions[New NodeDefinitions]
    Registry[INodeRegistryService]
    Cache[Definition Cache]
    CtxMenu[ContextMenu Component]
    
    PluginAssembly --> Discovery
    Discovery --> Definitions
    Definitions --> Registry
    Registry --> Cache
    
    Registry -.RegistryChanged.-> CtxMenu
    Cache --> CtxMenu
    
    style Registry fill:#4a90e2,color:#fff
    style CtxMenu fill:#50c878,color:#fff
```

**When a plugin is loaded:**

1. **Lifecycle** runs: `OnLoadAsync()` → `ConfigureServices()`
2. **Registry** receives node definitions (via `Register()` and optional `INodeProvider`)
3. **Initialization** runs: `OnInitializeAsync()`
4. **Event** is raised: `RegistryChanged`
5. **ContextMenu** refreshes its node catalog
6. **User** sees new nodes in add menu

---

## Plugin Lifecycle Hooks

Plugins now support explicit lifecycle hooks and service registration. The loader calls them in order:

```mermaid
sequenceDiagram
    participant Loader as PluginLoader
    participant Plugin as INodePlugin
    participant PSR as IPluginServiceRegistry
    participant Registry as INodeRegistryService
    participant Host as IServiceProvider

    Loader->>Plugin: OnLoadAsync()
    Loader->>PSR: RegisterServices(pluginId, ConfigureServices)
    Loader->>Plugin: Register(registry)
    Loader->>Plugin: OnInitializeAsync(Host)
    Note over Loader,Plugin: Plugin is now active

    Loader->>Plugin: OnUnloadAsync()
    Loader->>Plugin: Unload()
    Loader->>PSR: RemoveServices(pluginId)
```

### Lifecycle Guarantees

- `OnLoadAsync()` is called after the assembly is loaded and validated.
- `ConfigureServices()` is invoked to register plugin services into a plugin-owned service provider.
- `Register()` is called to register node definitions with the registry.
- `OnInitializeAsync()` is called with the host `IServiceProvider` (use for shared services).
- `OnUnloadAsync()` runs before `Unload()` and before unloading the plugin context.

---

## Plugin Event Bus

Plugins can subscribe to editor events using `IPluginEventBus` (scoped). It is wired to `NodeEditorState` events.

```mermaid
sequenceDiagram
    participant State as NodeEditorState
    participant Bus as IPluginEventBus
    participant Plugin as Plugin Subscriber

    Plugin->>Bus: SubscribeNodeAdded(handler)
    Plugin->>Bus: SubscribeConnectionAdded(handler)

    State->>Bus: NodeAdded event
    Bus->>Plugin: handler(NodeEventArgs)

    State->>Bus: ConnectionAdded event
    Bus->>Plugin: handler(ConnectionEventArgs)
```

### Event Coverage

The event bus publishes all core state changes:

- `NodeAdded`
- `NodeRemoved`
- `ConnectionAdded`
- `ConnectionRemoved`
- `SelectionChanged`
- `ConnectionSelectionChanged`
- `ViewportChanged`
- `ZoomChanged`
- `SocketValuesChanged`
- `NodeExecutionStateChanged`

When the bus is disposed, it unhooks from `NodeEditorState` to avoid memory leaks.

---

## Serialization & Deserialization Flow

### Save Graph Flow

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant State as NodeEditorState
    participant Serializer as GraphSerializer
    participant JSON as JSON Serializer
    participant File as File System
    
    User->>Canvas: Clicks "Save Graph"
    Canvas->>Serializer: SerializeAsync(State)
    
    Serializer->>State: Get Nodes
    Serializer->>State: Get Connections
    
    Serializer->>Serializer: Build GraphDto
    Note over Serializer: Convert ViewModels to DTOs
    
    Serializer->>JSON: JsonSerializer.Serialize(graphDto)
    JSON-->>Serializer: JSON string
    
    Serializer-->>Canvas: Return JSON
    Canvas->>File: Write to disk
    
    Canvas->>User: Show "Graph saved" notification
```

### Load Graph Flow with Migration

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant File as File System
    participant Serializer as GraphSerializer
    participant Migrator as GraphSchemaMigrator
    participant Validator as ConnectionValidator
    participant State as NodeEditorState
    participant Registry as INodeRegistryService
    
    User->>Canvas: Clicks "Load Graph"
    Canvas->>File: Read file contents
    File-->>Canvas: JSON string
    
    Canvas->>Serializer: DeserializeAsync(json)
    Serializer->>Serializer: Parse JSON to GraphDto
    Serializer->>Serializer: Check dto.Version
    
    alt Version < Current
        Serializer->>Migrator: Migrate(dto, currentVersion)
        
        loop For each version gap
            Migrator->>Migrator: ApplyMigration(v -> v+1)
            Note over Migrator: Transform node data, connection data
        end
        
        Migrator-->>Serializer: Migrated GraphDto
    end
    
    Serializer->>Registry: Get NodeDefinitions
    
    loop For each node in DTO
        Serializer->>Registry: GetDefinition(node.DefinitionId)
        alt Definition exists
            Serializer->>Serializer: Create NodeViewModel
        else Definition missing
            Serializer->>Serializer: Add to errors list
        end
    end
    
    loop For each connection in DTO
        Serializer->>Validator: CanConnect(source, target)
        alt Valid
            Serializer->>Serializer: Create ConnectionData
        else Invalid
            Serializer->>Serializer: Add to warnings list
        end
    end
    
    Serializer->>Serializer: Build GraphImportResult
    Serializer-->>Canvas: Return result
    
    alt Import successful
        Canvas->>State: Clear()
        
        loop For each node
            Canvas->>State: AddNode(node)
        end
        
        loop For each connection
            Canvas->>State: AddConnection(connection)
        end
        
        Canvas->>User: Show "Graph loaded" notification
    else Import has errors
        Canvas->>User: Show error dialog with details
    end
```

### Schema Migration Example

**Migration from V2 to V3** (adding execution path support):

```csharp
V2 GraphDto:
{
  "version": 2,
  "nodes": [
    {
      "id": "node-1",
      "definitionId": "Add",
      "inputs": [
        { "name": "a", "value": 5 },
        { "name": "b", "value": 3 }
      ],
      "outputs": [
        { "name": "Result" }
      ]
    }
  ]
}

V3 GraphDto (after migration):
{
  "version": 3,
  "nodes": [
    {
      "id": "node-1",
      "definitionId": "Add",
      "inputs": [
        { "name": "a", "value": 5, "type": "System.Int32" },
        { "name": "b", "value": 3, "type": "System.Int32" },
        { "name": "In", "type": "ExecutionPath" }  // ← ADDED
      ],
      "outputs": [
        { "name": "Result", "type": "System.Int32" },
        { "name": "Out", "type": "ExecutionPath" }  // ← ADDED
      ]
    }
  ]
}
```

---

## Viewport & Coordinate Transformation

### Coordinate System Translation

```mermaid
graph LR
    subgraph "Screen Space"
        MouseX[Mouse X, Y]
        ScreenRect[Canvas Bounding Rect]
    end
    
    subgraph "CoordinateConverter"
        Offset[Subtract Viewport Offset]
        Scale[Divide by Zoom]
    end
    
    subgraph "Graph Space"
        GraphX[Graph X, Y]
        NodePos[Node Position]
    end
    
    MouseX --> ScreenRect
    ScreenRect --> Offset
    Offset --> Scale
    Scale --> GraphX
    GraphX --> NodePos
    
    style CoordinateConverter fill:#4a90e2,color:#fff
```

### Screen to Graph Conversion

```csharp
// User clicks at screen position (500, 300)
// Viewport is at (-1000, -500)
// Zoom is 1.5

ScreenToGraph(Point2D screenPos)
{
    // 1. Get canvas offset
    var canvasRect = await GetBoundingClientRect();
    
    // 2. Adjust for canvas position
    var relativeX = screenPos.X - canvasRect.Left;
    var relativeY = screenPos.Y - canvasRect.Top;
    
    // 3. Account for viewport pan
    var graphX = (relativeX / Zoom) - Viewport.X;
    var graphY = (relativeY / Zoom) - Viewport.Y;
    
    return new Point2D(graphX, graphY);
}

// Example:
// (500 / 1.5) - (-1000) = 333.33 + 1000 = 1333.33
```

### Pan & Zoom Flow

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant CC as CoordinateConverter
    participant State as NodeEditorState
    participant Culler as ViewportCuller
    participant CSS as CSS Transform
    
    User->>Canvas: Middle mouse drag
    Canvas->>Canvas: Calculate delta
    Canvas->>State: Viewport = new Point2D(x, y)
    State->>Canvas: ViewportChanged Event
    Canvas->>Culler: UpdateVisibleNodes(viewport)
    Culler-->>Canvas: List of visible nodes
    Canvas->>CSS: Update transform style
    CSS->>User: Visual update (pan)
    
    User->>Canvas: Scroll wheel
    Canvas->>Canvas: Calculate zoom delta
    Canvas->>State: Zoom = newZoom
    State->>Canvas: ZoomChanged Event
    Canvas->>CC: Adjust viewport to zoom at cursor
    Canvas->>Culler: UpdateVisibleNodes(viewport)
    Canvas->>CSS: Update transform style
    CSS->>User: Visual update (zoom)
```

### Viewport Transform CSS

The viewport is transformed using CSS:

```css
.ne-viewport {
    transform: translate(${Viewport.X}px, ${Viewport.Y}px) scale(${Zoom});
    transform-origin: top left;
}
```

**Effect:**
- All child elements (nodes, connections) are transformed together
- Hardware-accelerated by GPU
- Smooth pan and zoom experience

### Viewport Culling Optimization

```mermaid
graph TB
    AllNodes[All Nodes in State]
    GetViewport[Get Current Viewport Bounds]
    CalcVisible[Calculate Visible Rect + Margin]
    
    subgraph "For Each Node"
        CheckNode[Check Node Bounds]
        InView{Intersects Viewport?}
        AddVisible[Add to Visible List]
        Skip[Skip Node]
    end
    
    VisibleNodes[Return Visible Nodes]
    RenderOnly[Render Only Visible]
    
    AllNodes --> GetViewport
    GetViewport --> CalcVisible
    CalcVisible --> CheckNode
    CheckNode --> InView
    InView -->|Yes| AddVisible
    InView -->|No| Skip
    AddVisible --> VisibleNodes
    Skip --> VisibleNodes
    VisibleNodes --> RenderOnly
    
    style CalcVisible fill:#4a90e2,color:#fff
    style RenderOnly fill:#50c878,color:#fff
```

**Culling Benefits:**

- **Before**: 1000 nodes → 1000 DOM elements → slow rendering
- **After**: 1000 nodes → 50 visible → 50 DOM elements → fast rendering
- Only nodes in viewport (plus margin) are rendered
- Automatically updates on pan/zoom

---

## Event Debugging Tips

### Tracing Event Flow

```csharp
// Enable debug logging in NodeEditorState
protected void RaiseNodeAdded(NodeViewModel node)
{
    Console.WriteLine($"[STATE] NodeAdded: {node.Data.Id}");
    NodeAdded?.Invoke(this, new StateChangeEventArgs<NodeViewModel>(node));
}

// In component
private void OnNodeAdded(object? sender, StateChangeEventArgs<NodeViewModel> e)
{
    Console.WriteLine($"[CANVAS] Received NodeAdded: {e.Data.Data.Id}");
    UpdateVisibleNodes();
    StateHasChanged();
}
```

### Common Event Issues

| Issue | Cause | Solution |
|-------|-------|----------|
| **Events not firing** | No subscribers | Check `OnInitialized` subscriptions |
| **Multiple renders** | Multiple `StateHasChanged()` calls | Batch state changes, debounce |
| **Memory leaks** | Forgot to unsubscribe | Implement `IDisposable`, unsubscribe in `Dispose()` |
| **Stale state** | Reading cached value | Always access `State.Property` directly |

---

## Summary

NodeEditor.Blazor's wiring is built on these key principles:

1. **Centralized State**: `NodeEditorState` is the single source of truth
2. **Event-Driven**: All updates flow through events, never direct calls
3. **Dependency Injection**: Services are properly scoped and injected
4. **Reactive UI**: Components subscribe to state events and re-render automatically
5. **Isolated Concerns**: Each service has a single, clear responsibility

This architecture ensures:
- ✅ **Testability**: Mock dependencies easily
- ✅ **Maintainability**: Clear separation of concerns
- ✅ **Scalability**: Can handle large graphs efficiently
- ✅ **Extensibility**: Plugin system allows unlimited expansion
- ✅ **Performance**: Viewport culling and efficient rendering

Understanding these wiring patterns is essential for extending the framework or debugging issues.
