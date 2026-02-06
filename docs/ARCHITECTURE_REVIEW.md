# NodeEditorMax — Architecture Review & Improvement Plan

> **Date:** February 6, 2026  
> **Scope:** Full code review of `NodeEditor.Blazor` component library — strategy/swappability patterns, state–ViewModel coupling, headless execution path, and modernization roadmap.

---

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Current Architecture Summary](#2-current-architecture-summary)
3. [Design Patterns in Use](#3-design-patterns-in-use)
4. [Code Review: Swappability & Strategy Patterns](#4-code-review-swappability--strategy-patterns)
   - 4.1 [DI Registration Audit](#41-di-registration-audit)
   - 4.2 [Services WITH Interface Abstractions (Swappable)](#42-services-with-interface-abstractions-swappable)
   - 4.3 [Services WITHOUT Interface Abstractions (Not Swappable)](#43-services-without-interface-abstractions-not-swappable)
   - 4.4 [Sealed & Static Classes Affecting Extensibility](#44-sealed--static-classes-affecting-extensibility)
   - 4.5 [Component Coupling Analysis](#45-component-coupling-analysis)
5. [Code Review: State–ViewModel Coupling](#5-code-review-stateviewmodel-coupling)
   - 5.1 [The Problem](#51-the-problem)
   - 5.2 [What's Already Done Right](#52-whats-already-done-right)
   - 5.3 [Why It Matters for Headless Execution](#53-why-it-matters-for-headless-execution)
6. [Improvement Plan](#6-improvement-plan)
   - Phase 1: [GraphData Model & Headless Execution Path](#phase-1-graphdata-model--headless-execution-path)
   - Phase 2: [Extract Interfaces for Core Services](#phase-2-extract-interfaces-for-core-services)
   - Phase 3: [Extract Interfaces for Infrastructure Services](#phase-3-extract-interfaces-for-infrastructure-services)
   - Phase 4: [Convert Static Classes to Injectable Services](#phase-4-convert-static-classes-to-injectable-services)
   - Phase 5: [DI Cleanup & Deduplication](#phase-5-di-cleanup--deduplication)
   - Phase 6: [Canvas Decomposition (Optional)](#phase-6-canvas-decomposition-optional)
7. [File-by-File Reference](#7-file-by-file-reference)
8. [Scorecard](#8-scorecard)

---

## 1. Project Overview

NodeEditor.Blazor is a **Blazor component library** for building visual node-based editors. Key capabilities:

- **Node creation** — nodes discovered via `[Node]` attributes on `INodeContext` methods, registered through `NodeRegistryService`
- **Execution pipelines** — sequential and parallel execution modes via `NodeExecutionService` + `ExecutionPlanner`
- **UI components** — canvas, node rendering, socket editors, context menus, connection paths, viewport culling
- **Plugin system** — dynamic loading via `PluginLoader` + `INodePlugin`, marketplace support, event bus
- **Serialization** — JSON graph import/export via `GraphSerializer` with schema migration

**Target workflow** (including future headless use):
1. Create/edit graphs in Blazor UI or via MCP tools
2. Serialize graphs to JSON
3. Import and execute graphs headlessly in code (no UI required)

---

## 2. Current Architecture Summary

### Layer Diagram

```
┌───────────────────────────────────────────────────────────────┐
│                     Razor Components                           │
│  NodeEditorCanvas, NodeComponent, ConnectionPath, SocketComponent│
│  ContextMenu, NodePropertiesPanel, VariablesPanel              │
│  (all inject concrete services directly)                       │
└──────────────────────────┬────────────────────────────────────┘
                           │ @inject / [CascadingParameter]
┌──────────────────────────▼────────────────────────────────────┐
│                     ViewModels                                 │
│  NodeViewModel (wraps NodeData + Position/Size/Selection)      │
│  SocketViewModel (wraps SocketData + mutable Value)            │
│  ViewModelBase (INotifyPropertyChanged)                        │
└──────────────────────────┬────────────────────────────────────┘
                           │ .Data property
┌──────────────────────────▼────────────────────────────────────┐
│                     Models (Pure Records)                       │
│  NodeData, SocketData, ConnectionData, GraphVariable           │
│  Point2D, Size2D, Rect2D, SocketValue, NodeDefinition          │
└──────────────────────────┬────────────────────────────────────┘
                           │
┌──────────────────────────▼────────────────────────────────────┐
│                     Services                                   │
│  NodeEditorState (central store, holds NodeViewModel[])        │
│  NodeRegistryService, NodeDiscoveryService                     │
│  SocketTypeResolver, ConnectionValidator                       │
│  NodeExecutionService, ExecutionPlanner                        │
│  GraphSerializer, GraphSchemaMigrator                          │
│  PluginLoader, PluginEventBus, PluginServiceRegistry           │
│  CoordinateConverter, ViewportCuller, TouchGestureHandler       │
│  VariableNodeFactory                                           │
└───────────────────────────────────────────────────────────────┘
```

### Key Relationships

- **NodeEditorState** is the central hub — holds `ObservableCollection<NodeViewModel>`, fires C# events, consumed by every component and most services.
- **Components** subscribe/unsubscribe to state events in lifecycle methods (Observer pattern).
- **Execution** is properly decoupled — `NodeExecutionService.ExecuteAsync()` takes `IReadOnlyList<NodeData>` + `IReadOnlyList<ConnectionData>` (pure models).
- **Serialization** is NOT decoupled — `GraphSerializer` reads from / writes to `NodeViewModel` objects through `NodeEditorState`.

---

## 3. Design Patterns in Use

| Pattern | Where | Assessment |
|---------|-------|------------|
| **Observer** | `NodeEditorState` events → Components subscribe/unsubscribe | ✅ Well-implemented, dominant pattern |
| **Strategy** | `INodeCustomEditor` — 8 implementations, first-match resolution via `NodeEditorCustomEditorRegistry` | ✅ Good extensibility point |
| **Chain of Responsibility** | `NodeEditorCustomEditorRegistry.GetEditor()` — tries each editor until `CanHandle()` returns true | ✅ Clean pattern |
| **Factory** | `NodeDiscoveryService` scans assemblies for `INodeContext` types; `NodeDefinition.Factory` delegate | ✅ Functional |
| **Composite** | `AggregatedNodeExecutionContext` combines multiple `INodeContext` instances | ✅ Good |
| **Adapter** | `NodeAdapter` (static) — converts legacy format to `NodeData` | ⚠️ Static, not injectable |
| **Pub/Sub** | `PluginEventBus` bridges `NodeEditorState` events to plugin-safe API | ✅ Well-designed |
| **Command** | ❌ Not implemented — undo/redo not available | Gap |
| **Mediator** | ❌ Not implemented — components interact with state directly | Acceptable for now |
| **Repository** | ❌ Not implemented — state uses `ObservableCollection` directly | Acceptable for now |

---

## 4. Code Review: Swappability & Strategy Patterns

### 4.1 DI Registration Audit

File: `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs` (~159 lines)

Out of **~30 distinct service registrations**, only **~10 use interface-based registration**. The remaining ~20 are registered and resolved by concrete type. Additionally, the two `AddNodeEditor` overloads duplicate nearly identical registration blocks — any new service must be added in both places.

### 4.2 Services WITH Interface Abstractions (Swappable) ✅

| Service | Interface | Lifetime | Notes |
|---------|-----------|----------|-------|
| `PluginServiceRegistry` | `IPluginServiceRegistry` | Singleton | Clean plugin service isolation |
| `PluginEventBus` | `IPluginEventBus` | Scoped | Bridges state events to plugins |
| `NodeContextRegistry` | `INodeContextRegistry` | Singleton | Plugin context registration |
| 8× Custom Editors | `INodeCustomEditor` | Singleton | Strategy pattern, multi-registration |
| `FileBasedMarketplaceCache` | `IPluginMarketplaceCache` | Singleton | |
| `TokenBasedAuthProvider` | `IPluginMarketplaceAuthProvider` | Scoped | |
| `LocalPluginMarketplaceSource` | `IPluginMarketplaceSource` | Scoped | Multi-registration |
| `RemotePluginMarketplaceSource` | `IPluginMarketplaceSource` | Scoped | Multi-registration |
| `PluginInstallationService` | `IPluginInstallationService` | Scoped | |

### 4.3 Services WITHOUT Interface Abstractions (Not Swappable) ❌

| Service | Lifetime | Sealed? | Impact |
|---------|----------|---------|--------|
| **`NodeEditorState`** | Scoped | **Yes** | **CRITICAL** — Central state. Every component and service depends on concrete type. Cannot mock, swap, or decorate. |
| **`NodeRegistryService`** | Singleton | **Yes** | **CRITICAL** — Node catalog. Components and serializer depend on concrete type. |
| **`SocketTypeResolver`** | Singleton | **Yes** | **CRITICAL** — Type resolution. `ConnectionValidator` and `NodeDiscoveryService` depend on concrete type. |
| **`NodeExecutionService`** | Scoped | **Yes** | **CRITICAL** — Execution engine. Cannot swap execution strategy. |
| **`GraphSerializer`** | Scoped | **Yes** | **CRITICAL** — Serialization. Cannot swap format (JSON ↔ binary, etc.). |
| **`PluginLoader`** | Singleton | **Yes** | **CRITICAL** — Plugin loading. Cannot replace loading strategy. |
| `ConnectionValidator` | Scoped | **Yes** | Injected into canvas and serializer. |
| `CoordinateConverter` | Scoped | **Yes** | Injected into canvas. |
| `ViewportCuller` | Scoped | **Yes** | Injected into canvas. |
| `TouchGestureHandler` | Scoped | No | Injected into canvas. |
| `NodeDiscoveryService` | Singleton | **Yes** | Used by `NodeRegistryService`. |
| `ExecutionPlanner` | Singleton | **Yes** | Used by `NodeExecutionService`. |
| `BackgroundExecutionQueue` | Singleton | **Yes** | Channel-based job queue. |
| `BackgroundExecutionWorker` | Scoped | Unknown | Dequeues/executes jobs. |
| `GraphSchemaMigrator` | Singleton | **Yes** | Schema versioning. |
| `NodeEditorCustomEditorRegistry` | Singleton | **Yes** | Aggregates `INodeCustomEditor` instances. |
| `VariableNodeFactory` | Scoped | **Yes** | Creates Get/Set variable nodes. |
| `AggregatedPluginMarketplaceSource` | Scoped | **Yes** | Aggregates marketplace sources. |

### 4.4 Sealed & Static Classes Affecting Extensibility

#### Static Classes (not injectable, not mockable)

| Class | File | Concern |
|-------|------|---------|
| `NodeEditorServiceExtensions` | `Services/NodeEditorServiceExtensions.cs` | DI setup — expected for extension methods |
| `NodeAdapter` | `Adapters/NodeAdapter.cs` | Static mapper — cannot extend for new snapshot formats |
| `NodeContextFactory` | `Services/Execution/NodeContextFactory.cs` | Static factory — not injectable, scans all loaded assemblies |
| `PlatformGuard` | `Services/PlatformGuard.cs` | Static utility — acceptable for guards |
| `SocketTypeDescriptor` | `Services/SocketTypeDescriptor.cs` | Static — not testable in isolation |

#### Sealed Classes (20+)

Nearly every service class is `sealed`. While this is good for performance, combined with the absence of interfaces it **prevents both inheritance and substitution** — no Decorator pattern, no test doubles, no alternative implementations.

### 4.5 Component Coupling Analysis

#### `NodeEditorCanvas.razor`

The canvas directly `@inject`s **5 concrete services**:

```razor
@inject CoordinateConverter CoordinateConverter
@inject ConnectionValidator ConnectionValidator
@inject TouchGestureHandler TouchGestures
@inject ViewportCuller ViewportCuller
@inject NodeRegistryService _registry
```

Plus `NodeEditorState` comes in as a `[Parameter]`:

```csharp
[Parameter, EditorRequired]
public NodeEditorState State { get; set; } = null!;
```

And is passed as a `CascadingValue` to child components:

```razor
<CascadingValue Value="State">
    <NodeComponent ... />
</CascadingValue>
```

**Every dependency is a concrete type.** None can be replaced without modifying the component. The canvas is also ~865 lines handling 8+ concerns (panning, zooming, selection, context menus, connections, culling, touch, plugin UI).

#### `NodeComponent.razor`

Takes `NodeEditorState` as a `[CascadingParameter]`:

```csharp
[CascadingParameter]
private NodeEditorState? EditorState { get; set; }
```

Directly calls `State.SelectNode()`, `State.RemoveNode()`, etc. Tightly coupled to concrete state class.

---

## 5. Code Review: State–ViewModel Coupling

### 5.1 The Problem

`NodeEditorState` stores nodes as `ObservableCollection<NodeViewModel>`:

```csharp
// NodeEditorState.cs
public ObservableCollection<NodeViewModel> Nodes { get; } = new();
public ObservableCollection<ConnectionData> Connections { get; } = new();  // ← pure model
public ObservableCollection<GraphVariable> Variables { get; } = new();    // ← pure model
```

Note the **asymmetry** — connections and variables are pure data records, but nodes are ViewModels.

`NodeViewModel` wraps `NodeData` (an immutable record) but adds UI-only state:

```csharp
// NodeViewModel.cs
public sealed class NodeViewModel : ViewModelBase  // INotifyPropertyChanged
{
    public NodeData Data { get; }                    // the wrapped model
    public IReadOnlyList<SocketViewModel> Inputs { get; }
    public IReadOnlyList<SocketViewModel> Outputs { get; }
    public Point2D Position { get; set; }            // UI-only: canvas position
    public Size2D Size { get; set; }                 // UI-only: rendered size
    public bool IsSelected { get; set; }             // UI-only: selection highlight
    public bool IsExecuting { get; set; }            // UI-only: execution animation
    public bool IsError { get; set; }                // UI-only: error highlight
}
```

Where `NodeData` is:

```csharp
// NodeData.cs
public sealed record class NodeData(
    string Id, string Name, bool Callable, bool ExecInit,
    IReadOnlyList<SocketData> Inputs, IReadOnlyList<SocketData> Outputs,
    string? DefinitionId = null);
```

And `SocketData` is:

```csharp
// SocketData.cs
public sealed record class SocketData(
    string Name, string TypeId, bool IsInput, bool IsExecution,
    SocketValue? Value = null, SocketEditorHint? EditorHint = null);
```

### Concrete problems caused by this coupling

| Problem | Evidence | Impact |
|---------|----------|--------|
| **No headless graph** | `NodeEditorState.Nodes` is `ObservableCollection<NodeViewModel>`. Cannot construct a graph without creating ViewModels (with Position, Size, IsSelected, etc.). | Testing, CLI tooling, and headless execution all require UI ViewModel types. |
| **Position/Size live only on ViewModel** | `NodeData` has no position/size. `GraphSerializer.ToDto()` reads `node.Position` and `node.Size` from the VM. | A model-only graph loses spatial layout — cannot be serialized. |
| **Socket values are mutable VM state** | `SocketViewModel` mutates its wrapped `SocketData` record. `BuildExecutionNodes()` snapshots these at call time. | Timing dependency: execution sees whatever values the VM held at snapshot time. |
| **Event args carry ViewModels** | `NodeAddedEventArgs` wraps `NodeViewModel`, not `NodeData`. | Non-UI consumers are coupled to the UI layer. |
| **Serializer import creates ViewModels** | `GraphSerializer.Import()` constructs `NodeViewModel` objects and calls `State.AddNode()`. | Cannot deserialize a graph without the ViewModel layer. |

### 5.2 What's Already Done Right

The **execution pipeline is properly decoupled** — `NodeExecutionService.ExecuteAsync()` takes `IReadOnlyList<NodeData>` + `IReadOnlyList<ConnectionData>`:

```csharp
// NodeExecutionService.cs
public async Task ExecuteAsync(
    IReadOnlyList<NodeData> nodes,
    IReadOnlyList<ConnectionData> connections,
    INodeExecutionContext context, ...)
```

The bridge is `BuildExecutionNodes()` on `NodeEditorState`:

```csharp
// NodeEditorState.cs
public IReadOnlyList<NodeData> BuildExecutionNodes()
{
    return Nodes
        .Select(node => new NodeData(
            node.Data.Id, node.Data.Name, node.Data.Callable, node.Data.ExecInit,
            node.Inputs.Select(socket => socket.Data).ToList(),
            node.Outputs.Select(socket => socket.Data).ToList(),
            node.Data.DefinitionId))
        .ToList();
}
```

This snapshots current socket values from ViewModels into pure model records. The pattern is good and can be generalized.

### 5.3 Why It Matters for Headless Execution

The target workflow is:

1. Create graphs in Blazor UI or via MCP tools
2. Serialize to JSON
3. Import and execute in code without any UI

**Current deserialization path (requires UI layer):**

```
JSON → GraphSerializer.Deserialize() → GraphDto
     → GraphSerializer.Import(GraphDto) → creates NodeViewModel[] → State.AddNode()
     → State.BuildExecutionNodes() → NodeData[]
     → NodeExecutionService.ExecuteAsync(NodeData[], ConnectionData[])
```

**Required headless path (no UI layer):**

```
JSON → Deserialize to GraphData (pure model)
     → Extract NodeData[] + ConnectionData[] directly
     → NodeExecutionService.ExecuteAsync(NodeData[], ConnectionData[])
```

---

## 6. Improvement Plan

### Phase 1: GraphData Model & Headless Execution Path
**Priority: HIGHEST** — Unblocks the core headless execution use case.

#### Step 1.1: Create `GraphData` Record

A pure model representing a complete serializable graph, including layout data:

```csharp
// NodeEditor.Blazor/Models/GraphData.cs

namespace NodeEditor.Blazor.Models;

/// <summary>
/// Pure data representation of a complete node graph.
/// Supports both UI rendering (with layout) and headless execution.
/// </summary>
public sealed record class GraphData(
    IReadOnlyList<GraphNodeData> Nodes,
    IReadOnlyList<ConnectionData> Connections,
    IReadOnlyList<GraphVariable> Variables,
    int SchemaVersion = 1);

/// <summary>
/// A node with both its domain data and spatial layout.
/// Combines NodeData (execution) + position/size (rendering/serialization).
/// </summary>
public sealed record class GraphNodeData(
    NodeData Data,
    Point2D Position,
    Size2D Size);
```

**Why a wrapper record instead of adding fields to `NodeData`:**  
`NodeData` is a clean domain record used by the execution engine. Adding `Position`/`Size` to it would pollute the execution layer with layout concerns. `GraphNodeData` composes both without coupling them.

#### Step 1.2: Refactor `GraphSerializer` to Operate on `GraphData`

Split serialization into two layers:

```csharp
// Pure model serialization (no UI dependency)
public GraphData DeserializeToGraphData(string json);
public string SerializeGraphData(GraphData graphData);

// UI integration (bridges GraphData ↔ NodeEditorState)
public void Import(GraphData graphData);           // GraphData → State (creates VMs)
public GraphData ExportToGraphData();              // State → GraphData (reads VMs)

// Keep existing convenience methods for backward compat:
public void Import(GraphDto dto);                  // existing (delegates to above)
public GraphDto Export();                          // existing (delegates to above)
```

**Key change in `ToDto` / `FromDto`:**
- `ToDto(GraphNodeData node)` reads `Position`/`Size` from the record, not a ViewModel.
- `FromDto(NodeDto dto)` returns a `GraphNodeData`, not a `NodeViewModel`.

#### Step 1.3: Add `LoadFromGraphData` / `ExportToGraphData` to `NodeEditorState`

```csharp
// NodeEditorState.cs — new projection methods

/// <summary>
/// Exports current state to a pure model representation.
/// </summary>
public GraphData ExportToGraphData()
{
    var nodes = Nodes.Select(vm => new GraphNodeData(
        new NodeData(vm.Data.Id, vm.Data.Name, vm.Data.Callable, vm.Data.ExecInit,
            vm.Inputs.Select(s => s.Data).ToList(),
            vm.Outputs.Select(s => s.Data).ToList(),
            vm.Data.DefinitionId),
        vm.Position,
        vm.Size)).ToList();

    return new GraphData(nodes, Connections.ToList(), Variables.ToList());
}

/// <summary>
/// Loads a pure model graph into UI state (creates ViewModels).
/// </summary>
public void LoadFromGraphData(GraphData graphData)
{
    ClearAll();
    foreach (var variable in graphData.Variables)
        AddVariable(variable);
    foreach (var graphNode in graphData.Nodes)
    {
        var vm = new NodeViewModel(graphNode.Data) { Position = graphNode.Position, Size = graphNode.Size };
        AddNode(vm);
    }
    foreach (var conn in graphData.Connections)
        AddConnection(conn);
}
```

#### Step 1.4: Create a Headless Execution Helper

A lightweight static or injectable helper that goes straight from `GraphData` → execution:

```csharp
// NodeEditor.Blazor/Services/Execution/HeadlessGraphRunner.cs

namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Executes a GraphData directly without any UI state, ViewModels, or Blazor DI.
/// </summary>
public sealed class HeadlessGraphRunner
{
    private readonly NodeExecutionService _executionService;

    public HeadlessGraphRunner(NodeExecutionService executionService)
    {
        _executionService = executionService;
    }

    /// <summary>
    /// Execute a graph from its pure model representation.
    /// </summary>
    public async Task<INodeExecutionContext> ExecuteAsync(
        GraphData graphData,
        INodeExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var nodes = graphData.Nodes.Select(n => n.Data).ToList();
        var connections = graphData.Connections.ToList();
        var executionContext = context ?? new StandardNodeExecutionContext();

        // Seed variable defaults into context
        foreach (var variable in graphData.Variables)
        {
            if (variable.DefaultValue is not null)
                executionContext.SetVariable(variable.Name, variable.DefaultValue);
        }

        await _executionService.ExecuteAsync(nodes, connections, executionContext,
            cancellationToken: cancellationToken);

        return executionContext;
    }

    /// <summary>
    /// Convenience: load from JSON and execute.
    /// </summary>
    public async Task<INodeExecutionContext> ExecuteFromJsonAsync(
        string json,
        INodeExecutionContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var graphData = GraphSerializer.DeserializeToGraphData(json);
        return await ExecuteAsync(graphData, context, cancellationToken);
    }
}
```

#### Result: Two Parallel Paths

```
┌─ UI Path ──────────────────────────────────────────────────────────┐
│ JSON → GraphSerializer.DeserializeToGraphData() → GraphData        │
│      → State.LoadFromGraphData() → NodeViewModel[] → UI rendering  │
│                                                                     │
│ UI → State.ExportToGraphData() → GraphData                         │
│    → GraphSerializer.SerializeGraphData() → JSON                   │
└────────────────────────────────────────────────────────────────────┘

┌─ Headless Path ────────────────────────────────────────────────────┐
│ JSON → GraphSerializer.DeserializeToGraphData() → GraphData        │
│      → HeadlessGraphRunner.ExecuteAsync(GraphData)                 │
│      → NodeExecutionService.ExecuteAsync(NodeData[], ConnectionData[]) │
│ (no ViewModels, no NodeEditorState, no Blazor DI)                  │
└────────────────────────────────────────────────────────────────────┘
```

---

### Phase 2: Extract Interfaces for Core Services
**Priority: HIGH** — Enables swapping, mocking, and decorating the 6 most critical services.

#### Step 2.1: `INodeEditorState`

Extract from `NodeEditorState` (~772 lines). Include all public events, properties, and methods:

```csharp
// NodeEditor.Blazor/Services/INodeEditorState.cs

public interface INodeEditorState
{
    // Collections
    ObservableCollection<NodeViewModel> Nodes { get; }
    ObservableCollection<ConnectionData> Connections { get; }
    ObservableCollection<GraphVariable> Variables { get; }
    IReadOnlyList<NodeViewModel> SelectedNodes { get; }
    ConnectionData? SelectedConnection { get; }

    // Viewport
    double Zoom { get; set; }
    Point2D Pan { get; set; }

    // Events (all 15)
    event EventHandler<NodeAddedEventArgs>? NodeAdded;
    event EventHandler<NodeRemovedEventArgs>? NodeRemoved;
    event EventHandler<ConnectionAddedEventArgs>? ConnectionAdded;
    event EventHandler<ConnectionRemovedEventArgs>? ConnectionRemoved;
    event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    event EventHandler<ConnectionSelectionChangedEventArgs>? ConnectionSelectionChanged;
    event EventHandler<NodeExecutionStateChangedEventArgs>? NodeExecutionStateChanged;
    event EventHandler? ExecutionCompleted;
    event EventHandler? StateChanged;
    event EventHandler? ZoomChanged;
    event EventHandler? PanChanged;
    event EventHandler<VariableAddedEventArgs>? VariableAdded;
    event EventHandler<VariableRemovedEventArgs>? VariableRemoved;
    event EventHandler<VariableChangedEventArgs>? VariableChanged;
    event EventHandler? NodesCleared;

    // Node operations
    NodeViewModel AddNode(NodeViewModel node);
    void RemoveNode(string nodeId);
    void RemoveConnectionsForNode(string nodeId);
    void RemoveConnectionsToInput(string nodeId, string socketName);
    void RemoveConnectionsFromOutput(string nodeId, string socketName);

    // Connection operations
    ConnectionData AddConnection(ConnectionData connection);
    bool RemoveConnection(ConnectionData connection);
    void SelectConnection(ConnectionData connection, bool isSelected);
    void ClearConnectionSelection();

    // Selection operations
    void SelectNode(string nodeId, bool clearExisting);
    void ToggleNodeSelection(string nodeId);
    void ClearSelection();
    void SelectNodes(IEnumerable<string> nodeIds, bool clearExisting);
    void SelectAll();
    void DeleteSelectedNodes();
    void CopySelectedNodes();
    void PasteNodes();

    // Execution bridge
    IReadOnlyList<NodeData> BuildExecutionNodes();
    void ApplyExecutionContext(INodeExecutionContext context, bool applyOutputs, bool applyErrors, bool clearPrevious);
    void SetNodeExecuting(string nodeId, bool isExecuting);
    void SetNodeError(string nodeId, bool isError);
    void ClearAllExecutionState();

    // Variables
    GraphVariable AddVariable(GraphVariable variable);
    void RemoveVariable(string variableId);
    GraphVariable? UpdateVariable(GraphVariable variable);
    GraphVariable? GetVariable(string variableId);

    // Graph management (Phase 1 additions)
    GraphData ExportToGraphData();
    void LoadFromGraphData(GraphData graphData);
    void ClearAll();
}
```

**DI registration change:**

```csharp
// Before:
services.AddScoped<NodeEditorState>();

// After:
services.AddScoped<INodeEditorState, NodeEditorState>();
```

**Component update pattern:**

```razor
@* Before: *@
@inject NodeEditorState State

@* After: *@
@inject INodeEditorState State
```

```csharp
// Before:
[CascadingParameter]
private NodeEditorState? EditorState { get; set; }

// After:
[CascadingParameter]
private INodeEditorState? EditorState { get; set; }
```

**Unseal the class:**

```csharp
// Before:
public sealed class NodeEditorState { ... }

// After:
public class NodeEditorState : INodeEditorState { ... }
```

#### Step 2.2: `INodeRegistryService`

```csharp
public interface INodeRegistryService
{
    IReadOnlyDictionary<string, NodeDefinition> Definitions { get; }
    NodeCatalog NodeCatalog { get; }
    void DiscoverNodes();
    void RegisterDefinition(NodeDefinition definition);
    void RegisterAssembly(System.Reflection.Assembly assembly);
    void UnregisterAssembly(System.Reflection.Assembly assembly);
    int RemoveDefinitions(Func<NodeDefinition, bool> predicate);
    int DefinitionCount { get; }
    NodeCatalog GetCatalog(Func<NodeDefinition, bool>? filter = null);
}
```

#### Step 2.3: `ISocketTypeResolver`

```csharp
public interface ISocketTypeResolver
{
    void Register<T>(string typeName);
    void Register(string typeName, Type type);
    Type? Resolve(string typeName);
}
```

#### Step 2.4: `INodeExecutionService`

```csharp
public interface INodeExecutionService
{
    // Events
    event EventHandler<NodeExecutionEventArgs>? NodeExecutionStarted;
    event EventHandler<NodeExecutionEventArgs>? NodeExecutionCompleted;
    event EventHandler<NodeExecutionFailedEventArgs>? NodeExecutionFailed;
    event EventHandler? ExecutionStarted;
    event EventHandler? ExecutionFinished;
    event EventHandler<ExecutionLayerEventArgs>? LayerExecutionStarted;
    event EventHandler<ExecutionLayerEventArgs>? LayerExecutionCompleted;

    Task ExecuteAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        NodeExecutionOptions? options = null,
        CancellationToken cancellationToken = default);

    Task StopAsync();
    bool IsRunning { get; }
}
```

#### Step 2.5: `IGraphSerializer`

```csharp
public interface IGraphSerializer
{
    // Pure model operations (Phase 1)
    GraphData DeserializeToGraphData(string json);
    string SerializeGraphData(GraphData graphData);

    // UI-integrated operations
    GraphDto Export();
    void Import(GraphDto dto);
    string Serialize(GraphDto dto);
    GraphDto? Deserialize(string json);
}
```

#### Step 2.6: `IPluginLoader`

```csharp
public interface IPluginLoader
{
    Task LoadPluginAsync(string pluginPath);
    Task UnloadPluginAsync(string pluginId);
    Task ReloadPluginAsync(string pluginPath);
    IReadOnlyList<PluginManifest> LoadedPlugins { get; }
}
```

---

### Phase 3: Extract Interfaces for Infrastructure Services
**Priority: MEDIUM** — Decouples `NodeEditorCanvas` from concrete service types.

#### Step 3.1: `ICoordinateConverter`

```csharp
public interface ICoordinateConverter
{
    Point2D Pan { get; set; }
    double Zoom { get; set; }
    Point2D ScreenToCanvas(Point2D screenPoint);
    Point2D CanvasToScreen(Point2D canvasPoint);
    Rect2D GetVisibleCanvasRect(Size2D viewportSize);
    void ApplyPanDelta(Point2D delta);
    Rect2D CanvasRectToScreenRect(Rect2D canvasRect);
    void Reset();
}
```

#### Step 3.2: `IConnectionValidator`

```csharp
public interface IConnectionValidator
{
    bool CanConnect(SocketData output, SocketData input);
}
```

#### Step 3.3: `IViewportCuller`

```csharp
public interface IViewportCuller
{
    IReadOnlyList<NodeViewModel> GetVisibleNodes(
        IEnumerable<NodeViewModel> nodes, Rect2D visibleRect);
    IReadOnlyList<ConnectionData> GetVisibleConnections(
        IEnumerable<ConnectionData> connections,
        IEnumerable<NodeViewModel> nodes, Rect2D visibleRect);
}
```

#### Step 3.4: `ITouchGestureHandler`

```csharp
public interface ITouchGestureHandler
{
    TouchGestureResult ProcessTouchStart(TouchPoint[] points);
    TouchGestureResult ProcessTouchMove(TouchPoint[] points);
    TouchGestureResult ProcessTouchEnd(TouchPoint[] points);
    void Reset();
}
```

**Canvas after Phase 3:**

```razor
@inject ICoordinateConverter CoordinateConverter
@inject IConnectionValidator ConnectionValidator
@inject ITouchGestureHandler TouchGestures
@inject IViewportCuller ViewportCuller
@inject INodeRegistryService _registry
```

---

### Phase 4: Convert Static Classes to Injectable Services
**Priority: MEDIUM** — Makes factories and adapters mockable and replaceable.

#### Step 4.1: `NodeAdapter` → `INodeAdapter`

```csharp
// Before: static class NodeAdapter { static NodeData Adapt(...) }
// After:
public interface INodeAdapter
{
    NodeData Adapt(LegacyNodeSnapshot snapshot);
}

public class NodeAdapter : INodeAdapter
{
    public NodeData Adapt(LegacyNodeSnapshot snapshot) { ... }
}
```

#### Step 4.2: `NodeContextFactory` → `INodeContextFactory`

```csharp
// Before: static class NodeContextFactory { static AggregatedNodeExecutionContext Create() }
// After:
public interface INodeContextFactory
{
    AggregatedNodeExecutionContext Create();
}

public class NodeContextFactory : INodeContextFactory
{
    public AggregatedNodeExecutionContext Create()
    {
        // Same assembly-scanning logic, but now injectable and mockable
    }
}
```

Register in DI:

```csharp
services.AddSingleton<INodeContextFactory, NodeContextFactory>();
```

#### Step 4.3: `SocketTypeDescriptor` → `ISocketTypeDescriptor`

Similar extraction — the static helper becomes an injectable service.

---

### Phase 5: DI Cleanup & Deduplication
**Priority: MEDIUM** — Eliminates the duplicated registration blocks.

#### Step 5.1: Consolidate `AddNodeEditor` Overloads

```csharp
// Before: two overloads with duplicated registrations

// After:
public static IServiceCollection AddNodeEditor(this IServiceCollection services)
    => services.AddNodeEditor(_ => { });

public static IServiceCollection AddNodeEditor(
    this IServiceCollection services,
    Action<PluginOptions> configurePlugins)
{
    services.AddOptions<PluginOptions>().Configure(configurePlugins);

    // All registrations here — single source of truth
    services.AddSingleton<IPluginServiceRegistry, PluginServiceRegistry>();
    services.AddScoped<INodeEditorState, NodeEditorState>();
    services.AddSingleton<INodeRegistryService, NodeRegistryService>();
    services.AddSingleton<ISocketTypeResolver>(sp => {
        var resolver = new SocketTypeResolver();
        // ... default type registrations
        return resolver;
    });
    // ... etc.
}
```

#### Step 5.2: Consider a Builder Pattern for Advanced Configuration

```csharp
// Future: fluent configuration API
services.AddNodeEditor(builder =>
{
    builder.UseExecutionService<CustomExecutionService>();
    builder.UseSerializer<BinaryGraphSerializer>();
    builder.ConfigurePlugins(opts => opts.PluginPaths = ["./plugins"]);
});
```

This is a future enhancement — the builder would internally call `services.AddScoped<INodeExecutionService, CustomExecutionService>()` etc.

---

### Phase 6: Canvas Decomposition (Optional)
**Priority: LOW** — Improves maintainability but not strictly required.

`NodeEditorCanvas.razor` is ~865 lines handling:
1. Pointer events (mouse down/move/up)
2. Touch gesture handling
3. Panning and zooming
4. Selection rectangle
5. Context menu
6. Variable drag-and-drop
7. Connection drawing
8. Socket position tracking
9. Plugin manager UI
10. Viewport culling

Consider extracting into sub-components or handler services:

```
NodeEditorCanvas.razor (coordinator, ~200 lines)
├── CanvasInteractionHandler.cs (pointer/touch events)
├── SelectionRectangle.razor (selection UI)
├── ConnectionDrawing.razor (in-progress connection line)
├── CanvasContextMenu.razor (right-click menu, already partially extracted)
└── PluginManagerOverlay.razor (plugin UI)
```

---

## 7. File-by-File Reference

### Services Layer

| File | Class | Sealed | Interface | Lines | Role |
|------|-------|--------|-----------|-------|------|
| `Services/NodeEditorServiceExtensions.cs` | `NodeEditorServiceExtensions` | Static | — | ~159 | DI registration |
| `Services/NodeEditorState.cs` | `NodeEditorState` | Sealed | **None** → `INodeEditorState` | ~772 | Central state store |
| `Services/SocketTypeResolver.cs` | `SocketTypeResolver` | Sealed | **None** → `ISocketTypeResolver` | ~31 | Type name → `Type` resolution |
| `Services/ConnectionValidator.cs` | `ConnectionValidator` | Sealed | **None** → `IConnectionValidator` | ~100 | Connection rule validation |
| `Services/CoordinateConverter.cs` | `CoordinateConverter` | Sealed | **None** → `ICoordinateConverter` | ~130 | Screen ↔ canvas coordinates |
| `Services/ViewportCuller.cs` | `ViewportCuller` | Sealed | **None** → `IViewportCuller` | ~80 | Visible node/connection filtering |
| `Services/TouchGestureHandler.cs` | `TouchGestureHandler` | No | **None** → `ITouchGestureHandler` | ~220 | Multi-touch gesture recognition |
| `Services/VariableNodeFactory.cs` | `VariableNodeFactory` | Sealed | **None** | ~130 | Creates Get/Set variable node definitions |
| `Services/SocketTypeDescriptor.cs` | `SocketTypeDescriptor` | Static | **None** | — | Socket type utility |
| `Services/PlatformGuard.cs` | `PlatformGuard` | Static | — | — | Platform checks |

### Execution Layer

| File | Class | Sealed | Interface | Lines | Role |
|------|-------|--------|-----------|-------|------|
| `Services/Execution/NodeExecutionService.cs` | `NodeExecutionService` | Sealed | **None** → `INodeExecutionService` | ~415 | Graph execution engine |
| `Services/Execution/ExecutionPlanner.cs` | `ExecutionPlanner` | Sealed | **None** | ~73 | Topological sort into layers |
| `Services/Execution/BackgroundExecutionQueue.cs` | `BackgroundExecutionQueue` | Sealed | **None** | ~22 | Channel-based job queue |
| `Services/Execution/BackgroundExecutionWorker.cs` | `BackgroundExecutionWorker` | — | **None** | ~25 | Dequeues and executes jobs |
| `Services/Execution/NodeContextFactory.cs` | `NodeContextFactory` | Static | **None** → `INodeContextFactory` | ~60 | Creates aggregated node context |
| `Services/Execution/INodeExecutionContext.cs` | `INodeExecutionContext` | Interface | ✅ | — | Socket values, variables, child scoping |
| `Services/Execution/StandardNodeExecutionContext.cs` | `StandardNodeExecutionContext` | Sealed | `INodeExecutionContext` | — | Dictionary-based implementation |
| `Services/Execution/HeadlessGraphRunner.cs` | *(to create)* | — | — | — | **Phase 1: direct GraphData → execution** |

### Serialization Layer

| File | Class | Sealed | Interface | Lines | Role |
|------|-------|--------|-----------|-------|------|
| `Services/Serialization/GraphSerializer.cs` | `GraphSerializer` | Sealed | **None** → `IGraphSerializer` | ~270 | JSON import/export |
| `Services/Serialization/GraphSchemaMigrator.cs` | `GraphSchemaMigrator` | Sealed | **None** | ~39 | Schema version upgrades |

### Registry Layer

| File | Class | Sealed | Interface | Lines | Role |
|------|-------|--------|-----------|-------|------|
| `Services/Registry/NodeRegistryService.cs` | `NodeRegistryService` | Sealed | **None** → `INodeRegistryService` | ~155 | Node definition catalog |
| `Services/Registry/NodeDiscoveryService.cs` | `NodeDiscoveryService` | Sealed | **None** | ~180 | Assembly scanning for `[Node]` methods |

### Plugin Layer

| File | Class | Sealed | Interface | Lines | Role |
|------|-------|--------|-----------|-------|------|
| `Services/Plugins/PluginLoader.cs` | `PluginLoader` | Sealed | **None** → `IPluginLoader` | ~505 | Dynamic plugin loading |
| `Services/Plugins/PluginEventBus.cs` | `PluginEventBus` | Sealed | `IPluginEventBus` ✅ | ~251 | State event → plugin bridge |
| `Services/Plugins/IPluginServiceRegistry.cs` | Interface | — | ✅ | ~12 | Per-plugin service isolation |
| `Services/Plugins/PluginServiceRegistry.cs` | `PluginServiceRegistry` | Sealed | `IPluginServiceRegistry` ✅ | — | Implementation |

### Editors Layer

| File | Class | Interface | Lines | Role |
|------|-------|-----------|-------|------|
| `Services/Editors/INodeCustomEditor.cs` | Interface | ✅ | ~12 | Strategy interface for socket editors |
| `Services/Editors/NodeEditorCustomEditorRegistry.cs` | `NodeEditorCustomEditorRegistry` | **None** | ~53 | Chain-of-responsibility resolver |
| `Services/Editors/*.cs` (8 files) | Various | `INodeCustomEditor` ✅ | — | Bool, Text, Numeric, Dropdown, etc. |

### Models

| File | Class | Type | Lines | Role |
|------|-------|------|-------|------|
| `Models/NodeData.cs` | `NodeData` | Sealed record | ~12 | Core node domain data |
| `Models/SocketData.cs` | `SocketData` | Sealed record | ~11 | Socket definition + value |
| `Models/ConnectionData.cs` | `ConnectionData` | Record | ~10 | Connection between sockets |
| `Models/GraphVariable.cs` | `GraphVariable` | Record | ~43 | Named typed variable |
| `Models/GraphData.cs` | *(to create)* | Record | — | **Phase 1: pure graph model** |
| `Models/GraphNodeData.cs` | *(to create)* | Record | — | **Phase 1: node + layout** |
| `Models/Point2D.cs` | `Point2D` | Readonly record struct | — | 2D point |
| `Models/Size2D.cs` | `Size2D` | Readonly record struct | — | 2D size |
| `Models/Rect2D.cs` | `Rect2D` | Readonly record struct | — | 2D rectangle |
| `Models/SocketValue.cs` | `SocketValue` | Sealed record | — | Typed value wrapper |
| `Models/NodeDefinition.cs` | `NodeDefinition` | Sealed record | — | Node factory + metadata |
| `Models/NodeCatalog.cs` | `NodeCatalog` | Sealed class | — | Hierarchical definition tree |
| `Models/GraphDto.cs` | Multiple DTOs | Records | — | Serialization DTOs |

### ViewModels

| File | Class | Lines | Role |
|------|-------|-------|------|
| `ViewModels/NodeViewModel.cs` | `NodeViewModel` | ~61 | Wraps `NodeData` + UI state |
| `ViewModels/SocketViewModel.cs` | `SocketViewModel` | ~37 | Wraps `SocketData` + mutable value |
| `ViewModels/ViewModelBase.cs` | `ViewModelBase` | — | `INotifyPropertyChanged` base |

### Adapters

| File | Class | Static | Interface | Role |
|------|-------|--------|-----------|------|
| `Adapters/NodeAdapter.cs` | `NodeAdapter` | **Static** | **None** → `INodeAdapter` | Legacy format conversion |

### Components

| File | Concrete Injections | Role |
|------|-------------------|------|
| `Components/NodeEditorCanvas.razor` | 5 concrete + `NodeEditorState` param | Main canvas (865 lines) |
| `Components/NodeComponent.razor` | `NodeEditorState` cascading param | Node rendering |
| `Components/ConnectionPath.razor` | None directly | Connection line rendering |
| `Components/SocketComponent.razor` | None directly | Socket port rendering |
| `Components/ContextMenu.razor` | — | Right-click menu |
| `Components/NodePropertiesPanel.razor` | — | Selected node properties |
| `Components/VariablesPanel.razor` | — | Variable management |

---

## 8. Scorecard

### Current State

| Dimension | Rating | Notes |
|-----------|--------|-------|
| Plugin system abstraction | ⭐⭐⭐⭐ | Good interface coverage for plugin contracts and marketplace |
| Custom editor extensibility | ⭐⭐⭐⭐ | Strategy/chain-of-responsibility via `INodeCustomEditor` |
| Observer/event architecture | ⭐⭐⭐⭐ | Well-implemented event-based state notifications |
| Execution layer decoupling | ⭐⭐⭐⭐ | `NodeExecutionService` takes pure models |
| Model/ViewModel separation | ⭐⭐⭐ | Clean separation, but state holds ViewModels not Models |
| Core service abstractions | ⭐⭐ | 6 critical services lack interfaces |
| Component decoupling | ⭐ | All Razor components depend on concrete types |
| Testability | ⭐⭐ | Static factories and sealed concrete DI make mocking difficult |
| DI hygiene | ⭐⭐ | Duplicated registration blocks, mostly concrete registrations |
| Headless execution | ⭐ | **Cannot execute without ViewModel layer** |

### After Full Implementation

| Dimension | Target | Change |
|-----------|--------|--------|
| Plugin system abstraction | ⭐⭐⭐⭐ | (unchanged) |
| Custom editor extensibility | ⭐⭐⭐⭐ | (unchanged) |
| Observer/event architecture | ⭐⭐⭐⭐ | (unchanged) |
| Execution layer decoupling | ⭐⭐⭐⭐⭐ | +`HeadlessGraphRunner` + `GraphData` model |
| Model/ViewModel separation | ⭐⭐⭐⭐ | +`GraphData`/`GraphNodeData` bridge layer |
| Core service abstractions | ⭐⭐⭐⭐⭐ | All 6 critical services have interfaces |
| Component decoupling | ⭐⭐⭐⭐ | All injections use interfaces |
| Testability | ⭐⭐⭐⭐ | Mockable interfaces + injectable factories |
| DI hygiene | ⭐⭐⭐⭐ | Single registration path, interface-based |
| Headless execution | ⭐⭐⭐⭐⭐ | **Full headless path: JSON → GraphData → Execute** |

---

## Implementation Order Summary

| Phase | Priority | Effort | Description |
|-------|----------|--------|-------------|
| **Phase 1** | 🔴 Highest | Medium | `GraphData` model + `GraphSerializer` refactor + `HeadlessGraphRunner` |
| **Phase 2** | 🟠 High | Large | Extract 6 core interfaces (`INodeEditorState`, `INodeRegistryService`, `ISocketTypeResolver`, `INodeExecutionService`, `IGraphSerializer`, `IPluginLoader`) + update all consumers |
| **Phase 3** | 🟡 Medium | Medium | Extract 4 infrastructure interfaces (`ICoordinateConverter`, `IConnectionValidator`, `IViewportCuller`, `ITouchGestureHandler`) |
| **Phase 4** | 🟡 Medium | Small | Convert 3 static classes to injectable services |
| **Phase 5** | 🟡 Medium | Small | DI deduplication + optional builder pattern |
| **Phase 6** | 🟢 Low | Large | Canvas decomposition into sub-components |

Each phase is independently valuable and can be shipped separately. Phase 1 is the critical path for headless execution; Phase 2 is the critical path for testability and extensibility.
