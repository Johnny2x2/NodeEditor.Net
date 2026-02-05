# NodeEditor.Blazor Architecture Documentation

## Overview

NodeEditor.Blazor is a comprehensive visual node editor framework built with Blazor, designed for creating and executing node-based graphs. The architecture follows an **event-based design pattern** with clear separation of concerns across several key layers.

## Table of Contents

1. [Core Architecture](#core-architecture)
2. [Dependency Injection Setup](#dependency-injection-setup)
3. [State Management](#state-management)
4. [Component Hierarchy](#component-hierarchy)
5. [Registry & Plugin System](#registry--plugin-system)
6. [Plugin Lifecycle & Services](#plugin-lifecycle--services)
7. [Execution Engine](#execution-engine)
8. [Serialization System](#serialization-system)
9. [Data Flow](#data-flow)

---

## Core Architecture

The system is organized into distinct layers, each with specific responsibilities:

```mermaid
graph TB
    subgraph "Presentation Layer"
        Canvas[NodeEditorCanvas]
        NodeComp[NodeComponent]
        SocketComp[SocketComponent]
        ConnPath[ConnectionPath]
        ContextMenu[ContextMenu]
    end
    
    subgraph "ViewModel Layer"
        NodeVM[NodeViewModel]
        SocketVM[SocketViewModel]
    end
    
    subgraph "State Management"
        State[NodeEditorState]
    end
    
    subgraph "Service Layer"
        Registry[NodeRegistryService]
        Execution[NodeExecutionService]
        Serializer[GraphSerializer]
        PluginLoader[PluginLoader]
        PluginEvents[IPluginEventBus]
        PluginServices[IPluginServiceRegistry]
        CoordConv[CoordinateConverter]
        ConnValid[ConnectionValidator]
        ViewCull[ViewportCuller]
    end
    
    subgraph "Model Layer"
        NodeData[NodeData]
        ConnectionData[ConnectionData]
        SocketData[SocketData]
        GraphDto[GraphDto]
    end
    
    Canvas --> State
    Canvas --> CoordConv
    Canvas --> ConnValid
    Canvas --> ViewCull
    
    NodeComp --> NodeVM
    SocketComp --> SocketVM
    
    NodeVM --> NodeData
    SocketVM --> SocketData
    
    State --> NodeData
    State --> ConnectionData
    
    Execution --> State
    Registry --> PluginLoader
    PluginServices --> PluginLoader
    PluginEvents --> State
    Serializer --> Registry
    Serializer --> State
    
    style State fill:#4a90e2,color:#fff
    style Canvas fill:#50c878,color:#fff
    style PluginEvents fill:#ff6b6b,color:#fff
    style PluginServices fill:#ff6b6b,color:#fff
```

### Architectural Principles

1. **Event-Driven Architecture**: State changes trigger events that UI components subscribe to
2. **Dependency Injection**: All services are registered through DI for testability
3. **MVVM Pattern**: Clear separation between View (Razor), ViewModel, and Model
4. **Plugin Architecture**: Extensible system for loading external node implementations
5. **Scoped vs Singleton Services**: Proper lifecycle management for different service types

---

## Dependency Injection Setup

All services are registered through the `AddNodeEditor()` extension method:

```mermaid
graph LR
    subgraph "Service Lifetimes"
        subgraph "Singleton Services"
            PL[PluginLoader]
            PSR[IPluginServiceRegistry]
            Reg[NodeRegistryService]
            Disc[NodeDiscoveryService]
            STR[SocketTypeResolver]
            EP[ExecutionPlanner]
            GSM[GraphSchemaMigrator]
            Editors[Custom Editors]
        end
        
        subgraph "Scoped Services"
            State[NodeEditorState]
            PEB[IPluginEventBus]
            CC[CoordinateConverter]
            CV[ConnectionValidator]
            TGH[TouchGestureHandler]
            VC[ViewportCuller]
            NES[NodeExecutionService]
            GS[GraphSerializer]
            BEW[BackgroundExecutionWorker]
        end
    end
    
    PL --> Reg
    PSR --> PL
    Disc --> Reg
    STR --> NES
    EP --> NES
    Reg --> GS
    State --> CC
    State --> CV
    State --> PEB
    
    style State fill:#4a90e2,color:#fff
    style NES fill:#ff6b6b,color:#fff
    style PEB fill:#ff6b6b,color:#fff
    style PSR fill:#ff6b6b,color:#fff
```

### Service Registration Pattern

```csharp
services.AddNodeEditor() registers:

Singleton Services (shared across application):
├── PluginLoader - manages plugin lifecycle
├── IPluginServiceRegistry - plugin service providers
├── NodeRegistryService - node definition catalog
├── NodeDiscoveryService - discovers nodes from assemblies
├── SocketTypeResolver - type compatibility checking
├── ExecutionPlanner - execution order planning
├── GraphSchemaMigrator - schema version migration
└── Custom Editors - text, numeric, bool editors

Scoped Services (per user/circuit):
├── NodeEditorState - central state management
├── IPluginEventBus - plugin event subscriptions
├── CoordinateConverter - screen/graph coordinate translation
├── ConnectionValidator - connection validation logic
├── TouchGestureHandler - touch/gesture processing
├── ViewportCuller - visibility optimization
├── NodeExecutionService - node execution orchestration
├── GraphSerializer - graph save/load
└── BackgroundExecutionWorker - async execution
```

---

## State Management

The `NodeEditorState` class is the **single source of truth** for the entire node graph.

```mermaid
graph TB
    subgraph "NodeEditorState"
        Nodes[Nodes Collection]
        Connections[Connections Collection]
        Selection[Selected Node IDs]
        SelConn[Selected Connection]
        Zoom[Zoom Level]
        Viewport[Viewport Position]
    end
    
    subgraph "Events Published"
        NodeAdded[NodeAdded]
        NodeRemoved[NodeRemoved]
        ConnAdded[ConnectionAdded]
        ConnRemoved[ConnectionRemoved]
        SelectionChanged[SelectionChanged]
        ConnSelChanged[ConnectionSelectionChanged]
        ViewportChanged[ViewportChanged]
        ZoomChanged[ZoomChanged]
        SocketValChanged[SocketValuesChanged]
        ExecStateChanged[NodeExecutionStateChanged]
        UndoReq[UndoRequested]
        RedoReq[RedoRequested]
    end
    
    Nodes --> NodeAdded
    Nodes --> NodeRemoved
    Connections --> ConnAdded
    Connections --> ConnRemoved
    Selection --> SelectionChanged
    SelConn --> ConnSelChanged
    Viewport --> ViewportChanged
    Zoom --> ZoomChanged
    
    Canvas[NodeEditorCanvas]
    ExecService[NodeExecutionService]
    
    Canvas -.subscribes.-> NodeAdded
    Canvas -.subscribes.-> ConnAdded
    Canvas -.subscribes.-> ViewportChanged
    
    ExecService -.subscribes.-> NodeAdded
    ExecService -.subscribes.-> ConnAdded
    
    Canvas --modifies--> Nodes
    Canvas --modifies--> Connections
    ExecService --updates--> ExecStateChanged
    
    style Nodes fill:#4a90e2,color:#fff
    style Events fill:#ff6b6b,color:#fff
```

### Key State Management Methods

```csharp
State Management API:
├── Graph Manipulation
│   ├── AddNode(NodeViewModel)
│   ├── RemoveNode(Guid nodeId)
│   ├── AddConnection(ConnectionData)
│   └── RemoveConnection(ConnectionData)
├── Selection Management
│   ├── SelectNode(Guid nodeId, bool additive)
│   ├── ToggleSelectNode(Guid nodeId)
│   ├── SelectNodes(IEnumerable<Guid>)
│   ├── ClearSelection()
│   └── SelectConnection(ConnectionData)
├── Viewport Management
│   ├── Zoom { get; set; }
│   └── Viewport { get; set; }
├── Execution State
│   ├── BuildExecutionNodes()
│   ├── ApplyExecutionContext(INodeExecutionContext)
│   ├── SetNodeExecuting(Guid nodeId)
│   ├── SetNodeError(Guid nodeId, string error)
│   └── ResetNodeExecutionState(Guid? nodeId)
└── History
    ├── RequestUndo()
    └── RequestRedo()
```

### Event Flow Pattern

```mermaid
sequenceDiagram
    participant UI as NodeEditorCanvas
    participant State as NodeEditorState
    participant Listeners as Event Subscribers
    
    UI->>State: AddNode(newNode)
    State->>State: Nodes.Add(newNode)
    State->>Listeners: Raise NodeAdded Event
    Listeners->>UI: StateHasChanged()
    UI->>UI: Re-render affected components
```

---

## Component Hierarchy

The Blazor component tree is structured to reflect the visual hierarchy of the editor:

```mermaid
graph TB
    Canvas[NodeEditorCanvas.razor]
    
    subgraph "Canvas Children"
        SelectRect[Selection Rectangle]
        PluginBtn[Plugin Manager Button]
        CtxMenu[ContextMenu]
        PluginDialog[PluginManagerDialog]
        Viewport[Viewport Container]
    end
    
    subgraph "Viewport Children"
        SVGLayer[SVG Connections Layer]
        NodeLayer[HTML Nodes Layer]
    end
    
    subgraph "SVG Layer"
        ConnPath1[ConnectionPath]
        ConnPath2[ConnectionPath]
        PendingConn[Pending ConnectionPath]
    end
    
    subgraph "Node Layer"
        Node1[NodeComponent]
        Node2[NodeComponent]
        Node3[NodeComponent]
    end
    
    subgraph "NodeComponent Children"
        Header[Node Header]
        InputSockets[Input SocketComponents]
        OutputSockets[Output SocketComponents]
        PropsPanel[NodePropertiesPanel]
    end
    
    Canvas --> SelectRect
    Canvas --> PluginBtn
    Canvas --> CtxMenu
    Canvas --> PluginDialog
    Canvas --> Viewport
    
    Viewport --> SVGLayer
    Viewport --> NodeLayer
    
    SVGLayer --> ConnPath1
    SVGLayer --> ConnPath2
    SVGLayer --> PendingConn
    
    NodeLayer --> Node1
    NodeLayer --> Node2
    NodeLayer --> Node3
    
    Node1 --> Header
    Node1 --> InputSockets
    Node1 --> OutputSockets
    Node1 --> PropsPanel
    
    style Canvas fill:#50c878,color:#fff
    style Viewport fill:#4a90e2,color:#fff
```

### Component Responsibilities

| Component | Purpose | Key Features |
|-----------|---------|--------------|
| **NodeEditorCanvas** | Root container, handles all user interactions | Pointer events, keyboard shortcuts, viewport transformation, culling |
| **NodeComponent** | Renders individual nodes | Drag handling, socket layout, selection state, execution state visualization |
| **SocketComponent** | Renders input/output sockets | Connection points, value editing, type indicators |
| **ConnectionPath** | Renders bezier curves between sockets | SVG path generation, selection handling, visual feedback |
| **ContextMenu** | Add node menu | Searchable node catalog, category filtering |
| **NodePropertiesPanel** | Socket value editors | Inline editing for disconnected inputs |

### Viewport Transform & Culling

```mermaid
graph LR
    subgraph "Rendering Pipeline"
        AllNodes[All Nodes in State]
        Culler[ViewportCuller]
        VisibleNodes[Visible Nodes]
        RenderNodes[Rendered NodeComponents]
    end
    
    subgraph "Coordinate Systems"
        Screen[Screen Coordinates]
        CoordConv[CoordinateConverter]
        Graph[Graph Coordinates]
    end
    
    AllNodes --> Culler
    State[State.Viewport] --> Culler
    State --> Culler
    Culler --> VisibleNodes
    VisibleNodes --> RenderNodes
    
    Screen --> CoordConv
    CoordConv --> Graph
    Graph --> CoordConv
    CoordConv --> Screen
    
    style Culler fill:#ff6b6b,color:#fff
    style CoordConv fill:#ff6b6b,color:#fff
```

**Viewport Transform CSS:**
```css
transform: translate({X}px, {Y}px) scale({Zoom});
```

**Culling Strategy:**
- Only nodes within viewport bounds + margin are rendered
- Dramatically improves performance for large graphs
- Updated on viewport pan/zoom

---

## Registry & Plugin System

The registry system discovers, validates, and manages node definitions from assemblies.

```mermaid
graph TB
    subgraph "Plugin Discovery & Loading"
        PluginDir[Plugin Directory]
        Loader[PluginLoader]
        ServiceRegistry[IPluginServiceRegistry]
        Manifest[Plugin Manifest]
        Validation[Plugin Validation]
        LoadCtx[PluginLoadContext]
    end
    
    subgraph "Node Discovery"
        Assemblies[Assemblies]
        Discovery[NodeDiscoveryService]
        Reflection[Reflection Analysis]
        NodeDef[NodeDefinition]
    end
    
    subgraph "Registry"
        Registry[NodeRegistryService]
        Catalog[NodeCatalog]
        Definitions[Node Definitions Map]
    end
    
    subgraph "Node Providers"
        Standard[StandardNodeContext]
        Composite[CompositeNodeContext]
        Custom[Custom Contexts]
    end
    
    PluginDir --> Loader
    ServiceRegistry --> Loader
    Loader --> Manifest
    Manifest --> Validation
    Validation --> LoadCtx
    LoadCtx --> Assemblies
    
    Assemblies --> Discovery
    Discovery --> Reflection
    Reflection --> NodeDef
    NodeDef --> Registry
    
    Registry --> Catalog
    Registry --> Definitions
    
    Standard --> Discovery
    Composite --> Discovery
    Custom --> Discovery
    
    style Registry fill:#4a90e2,color:#fff
    style Discovery fill:#ff6b6b,color:#fff
```

## Plugin Lifecycle & Services

Plugins now support lifecycle hooks and scoped service registration. Each plugin can provide its own service provider via `IPluginServiceRegistry` and participate in editor lifecycle via `INodePlugin` hooks.

```mermaid
sequenceDiagram
    participant Loader as PluginLoader
    participant Plugin as INodePlugin
    participant PSR as IPluginServiceRegistry
    participant Host as IServiceProvider

    Loader->>Plugin: OnLoadAsync()
    Loader->>PSR: RegisterServices(pluginId, ConfigureServices)
    Loader->>Plugin: Register(registry)
    Loader->>Plugin: OnInitializeAsync(Host)

    Note over Loader,Plugin: Plugin active

    Loader->>Plugin: OnUnloadAsync()
    Loader->>Plugin: Unload()
    Loader->>PSR: RemoveServices(pluginId)
```

**Key Responsibilities**

- **`OnLoadAsync()`**: initialize resources after assembly load
- **`ConfigureServices()`**: register plugin-owned services
- **`Register()`**: register node definitions
- **`OnInitializeAsync()`**: access host DI services
- **`OnUnloadAsync()` / `Unload()`**: cleanup and shutdown

---

## Plugin Event Bus

Plugins can subscribe to editor state changes through `IPluginEventBus`, which is wired to `NodeEditorState` and registered as a scoped service. This provides a safe way for plugins to react to graph changes without coupling directly to UI components.

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

**Covered Events**

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

When the event bus is disposed, it unhooks from `NodeEditorState` to avoid memory leaks.

---

### Node Definition Structure

```csharp
NodeDefinition:
├── Id: string                    // Unique identifier
├── Name: string                  // Display name
├── Category: string              // Category for organization
├── Color: ColorValue             // Visual styling
├── Icon: string                  // SVG icon path
├── Description: string           // Tooltip/help text
├── MethodName: string            // Method to invoke
├── ContextType: Type             // Class containing method
├── Parameters: List<SocketData>  // Input definitions
└── Outputs: List<SocketData>     // Output definitions
```

### Plugin Loading Flow

```mermaid
sequenceDiagram
    participant App as Application
    participant Loader as PluginLoader
    participant PSR as IPluginServiceRegistry
    participant Registry as NodeRegistryService
    
    App->>Loader: LoadAndRegisterAsync(services)
    Loader->>Loader: DiscoverCandidates()
    
    loop For each plugin
        Loader->>Loader: LoadCandidate()
        Loader->>Loader: Validate(plugin, manifest)
        
        alt Valid Plugin
            Loader->>Loader: OnLoadAsync()
            Loader->>PSR: RegisterServices(pluginId, ConfigureServices)
            Loader->>Registry: Register(registry)
            Loader->>Loader: Register definitions (INodeProvider)
            Loader->>Loader: OnInitializeAsync(services)
            Registry->>Registry: Raise RegistryChanged Event
        end
    end
    
    Loader-->>App: LoadResults
```

### Node Discovery Process

The system uses **reflection** to find methods decorated with `[Node]` attribute:

```csharp
Discovery Process:
1. Scan assemblies for classes implementing INodeContext
2. Find public methods with [Node] attribute
3. Extract method signature (parameters → inputs, return → outputs)
4. Generate NodeDefinition with:
   - Socket definitions from parameter types
   - Execution socket (if async or has await)
   - Metadata from attribute properties
5. Register with NodeRegistryService
```

**Example Node Declaration:**
```csharp
public class StandardNodeContext : INodeContext
{
    [Node(Category = "Math", Color = "#4CAF50")]
    public int Add(int a, int b)
    {
        return a + b;
    }
}
```

Becomes:
```
NodeDefinition {
  Id: "StandardNodeContext.Add"
  Category: "Math"
  Color: "#4CAF50"
  Inputs: [
    SocketData { Name: "a", Type: "System.Int32" },
    SocketData { Name: "b", Type: "System.Int32" }
  ]
  Outputs: [
    SocketData { Name: "Result", Type: "System.Int32" }
  ]
}
```

---

## Execution Engine

The execution engine orchestrates the evaluation of node graphs with support for parallel and sequential execution modes.

```mermaid
graph TB
    subgraph "Execution Pipeline"
        Start[Execute Request]
        Planner[ExecutionPlanner]
        Plan[ExecutionPlan]
        Executor[NodeExecutionService]
        Complete[Execution Complete]
    end
    
    subgraph "Planning Phase"
        Topo[Topological Sort]
        Layers[Layer Assignment]
        ParGroups[Parallel Groups]
    end
    
    subgraph "Execution Phase"
        SeqExec[Sequential Executor]
        ParExec[Parallel Executor]
        NodeInvoker[NodeMethodInvoker]
        Context[NodeExecutionContext]
    end
    
    subgraph "State Updates"
        StateExec[SetNodeExecuting]
        StateSuccess[ApplyExecutionContext]
        StateError[SetNodeError]
    end
    
    Start --> Planner
    Planner --> Topo
    Topo --> Layers
    Layers --> ParGroups
    ParGroups --> Plan
    
    Plan --> Executor
    Executor --> SeqExec
    Executor --> ParExec
    
    SeqExec --> NodeInvoker
    ParExec --> NodeInvoker
    NodeInvoker --> Context
    
    NodeInvoker --> StateExec
    Context --> StateSuccess
    NodeInvoker --> StateError
    
    StateSuccess --> Complete
    StateError --> Complete
    
    style Planner fill:#4a90e2,color:#fff
    style Executor fill:#ff6b6b,color:#fff
```

### Execution Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| **Sequential** | Executes layers one at a time, nodes within layer in parallel | Default mode, ensures execution order |
| **Parallel** | Executes all independent nodes simultaneously | Maximum performance, order-independent graphs |
| **Planned** | Uses pre-computed execution plan | Repeated execution of same graph structure |

### Execution Planning

```mermaid
graph LR
    subgraph "Graph Topology"
        N1[Node 1]
        N2[Node 2]
        N3[Node 3]
        N4[Node 4]
        N5[Node 5]
        
        N1 --> N3
        N2 --> N3
        N3 --> N4
        N3 --> N5
    end
    
    subgraph "Execution Layers"
        L0[Layer 0: N1, N2]
        L1[Layer 1: N3]
        L2[Layer 2: N4, N5]
    end
    
    N1 -.->L0
    N2 -.-> L0
    N3 -.-> L1
    N4 -.-> L2
    N5 -.-> L2
    
    L0 ==> L1
    L1 ==> L2
    
    style L0 fill:#4a90e2,color:#fff
    style L1 fill:#50c878,color:#fff
    style L2 fill:#ff6b6b,color:#fff
```

**Planning Algorithm:**
```
1. Build dependency graph from connections
2. Perform topological sort to find valid execution order
3. Assign nodes to layers:
   - Layer 0: nodes with no dependencies
   - Layer N: nodes depending only on layers 0..N-1
4. Nodes within same layer can execute in parallel
```

### Node Execution Flow

```mermaid
sequenceDiagram
    participant UI as NodeEditorCanvas
    participant State as NodeEditorState
    participant Service as NodeExecutionService
    participant Planner as ExecutionPlanner
    participant Invoker as NodeMethodInvoker
    participant Context as INodeContext
    
    UI->>Service: ExecuteAsync(nodeId, mode)
    Service->>State: BuildExecutionNodes()
    State-->>Service: ExecutionNodeData[]
    
    Service->>Planner: BuildPlan(nodes, connections)
    Planner-->>Service: ExecutionPlan
    
    loop For each layer
        Service->>Service: Raise LayerStarted
        
        par Execute layer nodes in parallel
            Service->>Service: ResolveInputsAsync(node)
            Service->>State: SetNodeExecuting(nodeId)
            Service->>Invoker: InvokeAsync(definition, inputs)
            Invoker->>Context: MethodInfo.Invoke(context, args)
            Context-->>Invoker: Result
            Invoker-->>Service: NodeExecutionContext
            Service->>State: ApplyExecutionContext(context)
        end
        
        Service->>Service: Raise LayerCompleted
    end
    
    Service-->>UI: Execution Complete
    UI->>UI: StateHasChanged()
```

### Input Resolution

Before executing a node, the system resolves all input values:

```csharp
Input Resolution Strategy:
1. Check if socket has incoming connection
   YES → Get value from connected output socket
   NO  → Use socket's default/stored value
2. Perform type conversion if needed
3. Handle ExecutionPath sockets specially:
   - Determine next execution node
   - Manage execution flow control
```

### Execution Context

Each node execution receives a `NodeExecutionContext`:

```csharp
NodeExecutionContext:
├── NodeId: Guid                          // Current node
├── Inputs: Dictionary<string, object?>   // Resolved input values
├── Outputs: Dictionary<string, object?>  // Populated by node
├── Error: string?                        // Error message if failed
├── IsSuccess: bool                       // Execution status
└── ExecutionTime: TimeSpan               // Performance tracking
```

---

## Serialization System

The serialization system handles saving and loading graphs with schema migration support.

```mermaid
graph TB
    subgraph "Serialization"
        Graph[NodeEditorState]
        Serializer[GraphSerializer]
        DTO[GraphDto]
        JSON[JSON String]
        File[File System]
    end
    
    subgraph "Deserialization"
        LoadJSON[JSON String]
        ParseDTO[Parse to GraphDto]
        Migrator[GraphSchemaMigrator]
        Validate[ConnectionValidator]
        RestoreState[Restore to State]
    end
    
    Graph -->|Serialize| Serializer
    Serializer --> DTO
    DTO --> JSON
    JSON --> File
    
    File --> LoadJSON
    LoadJSON --> ParseDTO
    ParseDTO --> Migrator
    Migrator --> Validate
    Validate --> RestoreState
    RestoreState --> Graph
    
    style Serializer fill:#4a90e2,color:#fff
    style Migrator fill:#ff6b6b,color:#fff
```

### GraphDto Structure

```csharp
GraphDto:
├── Version: int                      // Schema version
├── Nodes: List<NodeData>
│   ├── Id: Guid
│   ├── DefinitionId: string         // Maps to NodeDefinition
│   ├── Position: Point2D
│   ├── Inputs: List<SocketData>
│   └── Outputs: List<SocketData>
├── Connections: List<ConnectionData>
│   ├── Id: Guid
│   ├── SourceNodeId: Guid
│   ├── SourceSocket: string         // Socket name
│   ├── TargetNodeId: Guid
│   └── TargetSocket: string
└── Metadata: Dictionary<string, object>
```

### Schema Migration

```mermaid
graph LR
    V1[Schema V1]
    V2[Schema V2]
    V3[Schema V3]
    Current[Current Version]
    
    Migrate1[Migration 1→2]
    Migrate2[Migration 2→3]
    
    V1 --> Migrate1
    Migrate1 --> V2
    V2 --> Migrate2
    Migrate2 --> V3
    V3 --> Current
    
    style Current fill:#50c878,color:#fff
```

**Migration Process:**
```csharp
Migration Pipeline:
1. Detect graph version from JSON
2. Apply migrations sequentially:
   - V1→V2: Added socket type information
   - V2→V3: Added execution path support
   - V3→V4: Added composite nodes
3. Validate all connections after migration
4. Return GraphImportResult with:
   - Migrated graph data
   - List of warnings/errors
   - Success status
```

### Serialization API

```csharp
GraphSerializer Methods:
├── SerializeAsync(NodeEditorState)
│   └── Returns: Task<string> (JSON)
├── DeserializeAsync(string json)
│   └── Returns: Task<GraphImportResult>
├── ExportAsync(NodeEditorState, string path)
│   └── Writes to file
└── ImportAsync(string path)
    └── Loads from file
```

---

## Data Flow

### Complete User Interaction Flow

```mermaid
sequenceDiagram
    participant User
    participant Canvas as NodeEditorCanvas
    participant State as NodeEditorState
    participant VM as NodeViewModel
    participant Service as NodeExecutionService
    
    User->>Canvas: Right-click canvas
    Canvas->>Canvas: Open ContextMenu
    User->>Canvas: Select "Add Math → Add"
    Canvas->>State: AddNode(new NodeViewModel)
    State->>State: Nodes.Add(node)
    State->>Canvas: Raise NodeAdded Event
    Canvas->>Canvas: StateHasChanged()
    
    User->>Canvas: Drag from Output Socket
    Canvas->>Canvas: Create Pending Connection
    User->>Canvas: Drop on Input Socket
    Canvas->>State: AddConnection(connectionData)
    State->>State: Connections.Add(connection)
    State->>Canvas: Raise ConnectionAdded Event
    Canvas->>Canvas: StateHasChanged()
    
    User->>Canvas: Click Execute
    Canvas->>Service: ExecuteAsync(nodeId)
    Service->>State: BuildExecutionNodes()
    Service->>Service: Plan & Execute
    
    loop For each node
        Service->>State: SetNodeExecuting(nodeId)
        State->>Canvas: Raise NodeExecutionStateChanged
        Canvas->>Canvas: Update visual state
        Service->>Service: Execute node method
        Service->>State: ApplyExecutionContext(results)
        State->>Canvas: Raise SocketValuesChanged
    end
    
    Service->>Canvas: Execution Complete
    Canvas->>Canvas: Final render
```

### Connection Validation Flow

```mermaid
graph TB
    DragStart[User Drags from Socket]
    Validator[ConnectionValidator]
    TypeCheck{Types Compatible?}
    DirectionCheck{Correct Direction?}
    ExistingCheck{Connection Exists?}
    Accept[Allow Connection]
    Reject[Reject Connection]
    
    DragStart --> Validator
    Validator --> TypeCheck
    TypeCheck -->|Yes| DirectionCheck
    TypeCheck -->|No| Reject
    DirectionCheck -->|Yes| ExistingCheck
    DirectionCheck -->|No| Reject
    ExistingCheck -->|No| Accept
    ExistingCheck -->|Yes| Reject
    
    style Accept fill:#50c878,color:#fff
    style Reject fill:#ff6b6b,color:#fff
```

**Validation Rules:**
```csharp
Connection is valid if:
1. Types are compatible (exact match or assignable)
2. Direction is correct (output → input)
3. No duplicate connection exists
4. Not connecting node to itself
5. Input socket accepts connections (some are constant-only)
```

---

## Key Design Patterns

### 1. Event-Based Architecture
- **State** publishes events when data changes
- **Components** subscribe and react with UI updates
- Decouples state management from presentation

### 2. MVVM (Model-View-ViewModel)
- **Model**: `NodeData`, `ConnectionData`, `SocketData`
- **ViewModel**: `NodeViewModel`, `SocketViewModel`
- **View**: Razor components
- Clear separation enables testing and maintains clean boundaries

### 3. Service Locator via DI
- All services registered through `IServiceCollection`
- Components receive dependencies via `@inject`
- Enables mocking for unit tests

### 4. Plugin Architecture
- Dynamic assembly loading via `AssemblyLoadContext`
- Interface-based contracts (`INodeContext`, `INodePlugin`)
- Isolated plugin contexts prevent interference

### 5. Strategy Pattern
- Multiple execution strategies (Sequential, Parallel, Planned)
- Pluggable custom editors (`INodeCustomEditor`)
- Extensible marketplace sources (`IPluginMarketplaceSource`)

### 6. Repository Pattern
- `NodeRegistryService` manages node definitions
- Centralized access to node catalog
- Supports runtime registration of new nodes

---

## Performance Optimizations

### Viewport Culling
```csharp
ViewportCuller:
- Only renders nodes within visible viewport bounds
- Adds margin for smooth scrolling experience
- Dramatically reduces DOM size for large graphs
- Updates culling rect on pan/zoom
```

### Connection Rendering
```csharp
SVG Layer:
- All connections rendered in single SVG element
- Hardware-accelerated by browser
- Bezier curves for smooth appearance
- Conditional rendering for pending connections
```

### Event Throttling
```csharp
Touch/Mouse Events:
- Throttled viewport updates
- Batch state changes where possible
- Debounced resize handlers
```

### Lazy Loading
```csharp
Plugin System:
- Plugins loaded on-demand
- Assemblies unloadable for hot reload
- Manifest-based discovery before full load
```

---

## Extension Points

### 1. Custom Nodes
Implement `INodeContext` and decorate methods with `[Node]`:

```csharp
public class MyCustomNodes : INodeContext
{
    [Node(Category = "Custom", Color = "#FF5722")]
    public async Task<string> FetchDataAsync(string url)
    {
        // Implementation
    }
}
```

### 2. Custom Socket Editors
Implement `INodeCustomEditor`:

```csharp
public class DatePickerEditor : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
        => socket.TypeName == typeof(DateTime).FullName && socket.IsInput && !socket.IsExecution;
    
    public RenderFragment Render(SocketEditorContext context)
    {
        // Return Blazor RenderFragment
    }
}
```

### 3. Custom Marketplace Sources
Implement `IPluginMarketplaceSource`:

```csharp
public class CustomMarketplace : IPluginMarketplaceSource
{
    public async Task<List<MarketplacePluginInfo>> GetAvailablePluginsAsync()
    {
        // Fetch from custom source
    }
}
```

### 4. Custom Serialization
Extend `GraphSerializer` for custom formats:

```csharp
public class CustomSerializer : GraphSerializer
{
    // Override methods for custom serialization logic
}
```

---

## Testing Strategy

### Unit Tests
- **Service Layer**: Mock dependencies, test business logic
- **ViewModels**: Test property change notifications
- **Validators**: Test connection rules
- **Serializer**: Test save/load with various schemas

### Integration Tests
- **Execution Engine**: Full graph execution scenarios
- **Plugin Loading**: Load and register test plugins
- **State Management**: Multi-step state changes

### Component Tests (bUnit)
- **NodeComponent**: Rendering, drag behavior
- **SocketComponent**: Connection interactions
- **NodeEditorCanvas**: Viewport transformations

---

## Future Enhancements

1. **Undo/Redo System**: Command pattern implementation
2. **Graph Templates**: Pre-built node graph libraries
3. **Debugging Tools**: Breakpoints, step execution
4. **Performance Profiler**: Node execution timing visualization
5. **Collaborative Editing**: Real-time multi-user support
6. **WebAssembly Support**: Client-side execution
7. **Custom Layout Algorithms**: Auto-arrange nodes
8. **Expression Evaluation**: Inline formula nodes

---

## Conclusion

NodeEditor.Blazor provides a robust, extensible foundation for building visual node-based editors. The event-driven architecture ensures reactive UI updates, while the plugin system allows unlimited extensibility. The execution engine supports complex workflows with parallel execution, and the serialization system ensures graphs can be saved and migrated across versions.

For implementation details, see the source code and unit tests in the repository.
