# Phase 2 — Discovery & Registry

> **Goal**: Rewrite node discovery to scan for `NodeBase` subclasses instead of `INodeContext` + `[Node]` methods. Extend `NodeDefinition` with the new fields needed by the class-based system.

## 2.1 Extend `NodeDefinition`

**File**: `NodeEditor.Net/Services/Registry/NodeDefinition.cs`

**Current** (record with 7 positional parameters):
```csharp
public sealed record class NodeDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    Func<NodeData> Factory);
```

**New** (extended with optional fields for the class-based system):
```csharp
public sealed record class NodeDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    Func<NodeData> Factory,
    Type? NodeType = null,
    Func<INodeExecutionContext, CancellationToken, Task>? InlineExecutor = null,
    IReadOnlyList<StreamSocketInfo>? StreamSockets = null);
```

| New field | Purpose |
|-----------|---------|
| `NodeType` | The `NodeBase` subclass `Type`. Used by the engine to create instances via `Activator.CreateInstance()` or DI. `null` for inline/lambda nodes. |
| `InlineExecutor` | For nodes created via `NodeBuilder.Create().OnExecute(lambda)`. The engine calls this directly instead of instantiating a class. `null` for class-based nodes. |
| `StreamSockets` | Metadata about streaming socket groups (item data socket → per-item exec socket → completed exec socket). Used by the engine to route `EmitAsync()`. |

**Backward-compatible**: All existing callers that pass 7 positional args still compile since the new parameters have defaults.

---

## 2.2 Rewrite `NodeDiscoveryService`

**File**: `NodeEditor.Net/Services/Registry/NodeDiscoveryService.cs`

**Current behavior** (to be replaced):
1. Scan assemblies for types implementing `INodeContext` or `INodeMethodContext`
2. For each type, scan methods for `[NodeAttribute]`
3. For each `[Node]` method, build a `NodeDefinition` from the method signature (params → sockets)
4. Return list of `NodeDefinition`s

**New behavior**:
1. Scan assemblies for non-abstract types inheriting `NodeBase`
2. For each type:
   a. Create a temporary instance via `Activator.CreateInstance()` (parameterless ctor required)
   b. Create a `NodeBuilder.CreateForType(type)` builder
   c. Call `instance.Configure(builder)`
   d. Call `builder.Build()` → `NodeDefinition`
   e. Dispose the temporary instance
3. Return list of `NodeDefinition`s

```csharp
using System.Reflection;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Net.Services.Registry;

/// <summary>
/// Discovers NodeBase subclasses from assemblies and builds NodeDefinitions
/// by calling Configure() on each.
/// </summary>
public sealed class NodeDiscoveryService
{
    /// <summary>
    /// Discovers all NodeBase subclasses in the given assemblies and returns their definitions.
    /// </summary>
    public IReadOnlyList<NodeDefinition> DiscoverFromAssemblies(IEnumerable<Assembly> assemblies)
    {
        var definitions = new List<NodeDefinition>();

        foreach (var assembly in assemblies)
        {
            if (assembly is null || assembly.IsDynamic) continue;

            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null || type.IsAbstract || !type.IsSubclassOf(typeof(NodeBase)))
                    continue;

                if (type.GetConstructor(Type.EmptyTypes) is null)
                    continue;

                try
                {
                    var definition = BuildDefinitionFromType(type);
                    if (definition is not null)
                        definitions.Add(definition);
                }
                catch
                {
                    // Skip types that cannot be instantiated or configured
                }
            }
        }

        return definitions;
    }

    /// <summary>
    /// Builds a NodeDefinition from a single NodeBase subclass type.
    /// </summary>
    public NodeDefinition? BuildDefinitionFromType(Type nodeType)
    {
        if (nodeType.IsAbstract || !nodeType.IsSubclassOf(typeof(NodeBase)))
            return null;

        // Create a temporary instance just to call Configure()
        var instance = (NodeBase)Activator.CreateInstance(nodeType)!;
        try
        {
            var builder = NodeBuilder.CreateForType(nodeType);
            instance.Configure(builder);
            return builder.Build();
        }
        finally
        {
            instance.OnDisposed();
        }
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t is not null)!; }
    }
}
```

**Key changes from current**:
- No more `IsContextType()` checking `INodeContext`/`INodeMethodContext`
- No more method scanning or `GetCustomAttribute<NodeAttribute>()`
- No more parameter-to-socket mapping logic (this moves into `NodeBuilder`)
- Much simpler — delegates all socket/metadata definition to the builder
- The `BuildDefinitionId` logic is now in `NodeBuilder.Build()` (Phase 1)

---

## 2.3 Update `NodeRegistryService`

**File**: `NodeEditor.Net/Services/Registry/NodeRegistryService.cs`

The registry's public API stays the same (`RegisterFromAssembly`, `RegisterDefinitions`, `RemoveDefinitions`, etc.), but the internal discovery delegation changes:

**Changes**:
- `RegisterFromAssembly(assembly)` → calls `NodeDiscoveryService.DiscoverFromAssemblies([assembly])` (which now scans for `NodeBase` subclasses)
- `EnsureInitialized(assemblies)` → same pattern, discover from all listed assemblies
- `RegisterPluginAssembly(assembly)` → same as `RegisterFromAssembly` but tracks as plugin definitions
- Everything else (`GetCatalog`, `RemoveDefinitions`, etc.) stays the same

**Method changes**:
```csharp
public void RegisterFromAssembly(Assembly assembly)
{
    var newDefinitions = _discoveryService.DiscoverFromAssemblies(new[] { assembly });
    RegisterDefinitions(newDefinitions);
}
```

No structural changes to the registry — it's already definition-centric. The definitions just come from a different source now.

---

## 2.4 Update `INodeRegistryService` interface

**File**: `NodeEditor.Net/Services/Registry/INodeRegistryService.cs`

**No changes needed** — the interface is already generic enough:
```csharp
void RegisterFromAssembly(Assembly assembly);
void RegisterDefinitions(IEnumerable<NodeDefinition> definitions);
int RemoveDefinitions(IEnumerable<NodeDefinition> definitions);
```

These work with `NodeDefinition`, which is the same record (just extended). No API changes.

---

## 2.5 Update `NodeCatalog`

**File**: `NodeEditor.Net/Services/Registry/NodeCatalog.cs`

**No changes needed** — `NodeCatalog` works with `NodeDefinition` only, grouping by `Category` and filtering by name. The extended fields (`NodeType`, `InlineExecutor`, `StreamSockets`) don't affect catalog behavior.

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **Modify** | `NodeEditor.Net/Services/Registry/NodeDefinition.cs` | Add 3 optional parameters |
| **Rewrite** | `NodeEditor.Net/Services/Registry/NodeDiscoveryService.cs` | Scan for `NodeBase` subclasses instead of `INodeContext` |
| **Modify** | `NodeEditor.Net/Services/Registry/NodeRegistryService.cs` | Update internal discovery call (minor) |
| **No change** | `NodeEditor.Net/Services/Registry/INodeRegistryService.cs` | Already generic |
| **No change** | `NodeEditor.Net/Services/Registry/NodeCatalog.cs` | Already generic |

## Dependencies

- Depends on Phase 1 (`NodeBase`, `NodeBuilder`, `StreamSocketInfo`)
- Phase 3 (execution engine) depends on the `NodeType`, `InlineExecutor` fields added here
- Phase 4 (standard nodes) depends on discovery working for `NodeBase` subclasses
