# Phase 7 — Blazor Integration & DI Updates

> **Goal**: Update the DI registration, UI integration points, serializer, and headless runner to work with the new class-based system. Most UI components are unaffected because they operate on `NodeDefinition` / `NodeData` / `NodeViewModel`, which are preserved.

## 7.1 Update `NodeEditorServiceExtensions`

**File**: `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs`

### Registrations to remove

| Old registration | Why removed |
|-----------------|-------------|
| `INodeContextFactory` → `NodeContextFactory` | Assembly scanning for `INodeContext` types — replaced by `NodeBase` discovery |
| `INodeContextRegistry` → `NodeContextRegistry` | Plugin context registration — replaced by direct assembly scanning |

### Registrations that stay (unchanged)

| Registration | Lifetime | Notes |
|---|---|---|
| `NodeDiscoveryService` | Singleton | Rewritten (Phase 2) but same registration |
| `INodeRegistryService` → `NodeRegistryService` | Singleton | Updated internals, same interface |
| `ISocketTypeResolver` → `SocketTypeResolver` | Singleton | Update: remove `ExecutionPath` pre-registration, add `ExecutionSocket` |
| `ExecutionPlanner` | Singleton | Simplified (Phase 3), same registration |
| `INodeExecutionService` → `NodeExecutionService` | Scoped | Rewritten (Phase 3), same interface |
| `IPluginLoader` → `PluginLoader` | Singleton | Updated (Phase 6) |
| `IPluginServiceRegistry` → `PluginServiceRegistry` | Singleton | Unchanged |
| `VariableNodeFactory` | Scoped | Updated to use `ExecutionSocket.TypeName` |
| `EventNodeFactory` | Scoped | Updated to use `ExecutionSocket.TypeName` |
| `INodeEditorState` → `NodeEditorState` | Scoped | Unchanged |
| `IPluginEventBus` → `PluginEventBus` | Scoped | Unchanged |

### Updated initialization

The `AddNodeEditor()` extension method should also register inline data node definitions after discovery:

```csharp
public static IServiceCollection AddNodeEditor(this IServiceCollection services)
{
    // ... existing singleton registrations ...

    // Remove:
    // services.AddSingleton<INodeContextFactory, NodeContextFactory>();
    // services.AddSingleton<INodeContextRegistry, NodeContextRegistry>();

    // Keep all other registrations...

    return services;
}
```

And in the initialization flow (called during startup):

```csharp
// In the registry initialization or a startup service:
registry.EnsureInitialized(assemblies); // Discovers NodeBase subclasses
registry.RegisterDefinitions(StandardNodeRegistration.GetInlineDefinitions()); // Registers lambda data nodes
```

---

## 7.2 UI Components — No changes

These components work with `NodeDefinition`, `NodeData`, `NodeViewModel`, `SocketViewModel`, `ConnectionData` — all preserved:

| Component | Why unaffected |
|-----------|---------------|
| `NodeEditorCanvas.razor` | Uses `definition.Factory()` → `NodeData` → `NodeViewModel` → `state.AddNode()`. The `Factory` delegate is produced by `NodeBuilder.Build()`, same shape. |
| `NodeComponent.razor` | Renders `NodeViewModel`. No coupling to execution system. |
| `ConnectionPath.razor` | Renders connections between sockets. No coupling to execution. |
| `SocketComponent.razor` | Renders socket UI. Uses `SocketData.IsExecution` flag (unchanged). |
| `CanvasInteractionHandler.cs` | Uses `definition.Factory()` for node stamping. Unchanged. |
| Context menu | Queries `registry.GetCatalog()` for node definitions. Unchanged. |

**The only potential UI issue**: If any component directly checked `typeof(ExecutionPath).FullName` for execution socket type identification. This would need to change to `ExecutionSocket.TypeName`. Verify with grep.

---

## 7.3 Update `SocketTypeResolver`

**File**: `NodeEditor.Net/Services/Core/SocketTypeResolver.cs`

**Changes**:
- Remove pre-registration of `ExecutionPath` type
- The resolver is still needed for data socket types (`int`, `string`, `double`, `SerializableList`, etc.)
- Execution sockets are now identified purely by `SocketData.IsExecution == true` flag, not by type resolution

```diff
  public SocketTypeResolver()
  {
-     Register(typeof(ExecutionPath));     // ← remove
      Register(typeof(SerializableList));
      // ... other type registrations
  }
```

---

## 7.4 Update `GraphSerializer`

**File**: `NodeEditor.Net/Services/Serialization/GraphSerializer.cs`

### Backward compatibility

The serializer works with `GraphData` (which contains `NodeData`, `SocketData`, `ConnectionData`). Key field: `NodeData.DefinitionId`.

**Old system**: `DefinitionId = "Namespace.Type.Method(ParamType1,ParamType2)"` (method signature-based)
**New system**: `DefinitionId = "Namespace.NodeBaseSubclass"` (class-based)

**Problem**: Existing saved graphs use old-style `DefinitionId`s. They won't match new-style definitions.

**Solution — Migration map**: Create a static mapping from old `DefinitionId` → new `DefinitionId` for all standard nodes:

```csharp
/// <summary>
/// Maps old method-based DefinitionIds to new class-based DefinitionIds
/// for backward-compatible graph deserialization.
/// </summary>
public static class DefinitionIdMigration
{
    private static readonly Dictionary<string, string> _map = new(StringComparer.Ordinal)
    {
        // Helpers
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.Start(NodeEditor.Net.Services.Execution.ExecutionPath&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.StartNode",
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.Marker(NodeEditor.Net.Services.Execution.ExecutionPath,NodeEditor.Net.Services.Execution.ExecutionPath&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.MarkerNode",

        // Conditions
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.Branch(NodeEditor.Net.Services.Execution.ExecutionPath,System.Boolean,NodeEditor.Net.Services.Execution.ExecutionPath&,NodeEditor.Net.Services.Execution.ExecutionPath&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.BranchNode",
        ["NodeEditor.Net.Services.Execution.StandardNodeContext.ForLoop(System.Int32,NodeEditor.Net.Services.Execution.ExecutionPath&,NodeEditor.Net.Services.Execution.ExecutionPath&,System.Int32&)"]
            = "NodeEditor.Net.Services.Execution.StandardNodes.ForLoopNode",

        // ... all other standard nodes ...

        // Data nodes (inline lambda — DefinitionId is the name)
        // Old: "NodeEditor.Net.Services.Execution.StandardNodeContext.Abs(System.Double)"
        // New: "Abs"
    };

    public static string Migrate(string definitionId)
    {
        return _map.TryGetValue(definitionId, out var newId) ? newId : definitionId;
    }
}
```

The serializer calls `DefinitionIdMigration.Migrate(nodeData.DefinitionId)` during deserialization before looking up definitions.

---

## 7.5 Update `HeadlessGraphRunner`

**File**: `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs`

**Changes**:
- Remove `INodeContextFactory` / `CompositeNodeContext` creation
- Remove `nodeContext` parameter from `ExecuteAsync()` (no longer needed — node instances are created per-node by the engine)
- Simplify to: extract nodes/connections → delegate to `_executionService.ExecuteAsync()`

```csharp
public sealed class HeadlessGraphRunner
{
    private readonly INodeExecutionService _executionService;
    private readonly IGraphSerializer _serializer;

    public HeadlessGraphRunner(
        INodeExecutionService executionService,
        IGraphSerializer serializer)
    {
        _executionService = executionService;
        _serializer = serializer;
    }

    public async Task ExecuteAsync(
        GraphData graphData,
        INodeRuntimeStorage? runtimeStorage = null,
        ExecutionOptions? options = null,
        CancellationToken ct = default)
    {
        runtimeStorage ??= new NodeRuntimeStorage();

        var nodes = graphData.Nodes;
        var connections = graphData.Connections;

        // Seed variables
        VariableNodeExecutor.SeedVariables(nodes, connections, runtimeStorage, graphData.Variables);

        await _executionService.ExecuteAsync(nodes, connections, runtimeStorage, options, ct);
    }

    public async Task ExecuteFromJsonAsync(string json, CancellationToken ct = default)
    {
        var graphData = _serializer.Deserialize(json);
        await ExecuteAsync(graphData, ct: ct);
    }
}
```

---

## 7.6 Update `NodeEditorState`

**File**: `NodeEditor.Net/Services/Core/NodeEditorState.cs`

### `BuildExecutionNodes()` — no change

```csharp
public IReadOnlyList<NodeData> BuildExecutionNodes()
{
    return Nodes
        .Select(node => new NodeData(
            node.Data.Id, node.Data.Name, node.Data.Callable, node.Data.ExecInit,
            node.Inputs.Select(s => s.Data).ToList(),
            node.Outputs.Select(s => s.Data).ToList(),
            node.Data.DefinitionId))
        .ToList();
}
```

This snapshots ViewModel state into immutable `NodeData` records — completely unchanged.

### `ApplyExecutionContext()` — minor update

Currently takes `INodeExecutionContext` (old) and maps socket values back to ViewModels. Update to take `INodeRuntimeStorage`:

```diff
- public void ApplyExecutionContext(INodeExecutionContext context)
+ public void ApplyExecutionContext(INodeRuntimeStorage runtimeStorage)
```

---

## 7.7 Update MCP Integration

**File**: `NodeEditor.Mcp/Abilities/NodeAbilityProvider.cs`

**No changes needed** — uses `definition.Factory()` to create `NodeData`, then wraps in `NodeViewModel`. The `Factory` delegate is produced by `NodeBuilder.Build()` with the same signature.

---

## 7.8 Update Adapter

**File**: `NodeEditor.Net/Adapters/NodeAdapter.cs`

**No changes needed** — converts `LegacyNodeSnapshot` → `NodeData`. This is a migration adapter from an even older format and doesn't interact with the execution system.

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **Modify** | `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs` | Remove `INodeContextFactory`, `INodeContextRegistry` registrations |
| **Modify** | `NodeEditor.Net/Services/Core/SocketTypeResolver.cs` | Remove `ExecutionPath` pre-registration |
| **Create** | `NodeEditor.Net/Services/Serialization/DefinitionIdMigration.cs` | Old→new DefinitionId mapping |
| **Modify** | `NodeEditor.Net/Services/Serialization/GraphSerializer.cs` | Apply DefinitionId migration during deserialization |
| **Modify** | `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs` | Simplify (remove CompositeNodeContext) |
| **Modify** | `NodeEditor.Net/Services/Core/NodeEditorState.cs` | `ApplyExecutionContext` → `INodeRuntimeStorage` |
| **No change** | `NodeEditor.Blazor/Components/NodeEditorCanvas.razor` | Uses `Factory` delegate (unchanged) |
| **No change** | `NodeEditor.Blazor/Components/NodeComponent.razor` | Renders ViewModel |
| **No change** | `NodeEditor.Blazor/Components/ConnectionPath.razor` | Renders connections |
| **No change** | `NodeEditor.Mcp/Abilities/NodeAbilityProvider.cs` | Uses `Factory` delegate |
| **No change** | `NodeEditor.Net/Adapters/NodeAdapter.cs` | Legacy migration |

## Dependencies

- Depends on Phase 1 (`ExecutionSocket.TypeName`, `INodeRuntimeStorage`)
- Depends on Phase 3 (execution engine rewrite)
- Depends on Phase 5 (old types deleted — DI must not register them)
- Can run partially in parallel with Phase 4 and Phase 6
