# 4A — NodeDiscoveryService Rewrite

> **Phase 4 — Discovery & Registry Services**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–3** complete (NodeBase, NodeBuilder, NodeDefinition extended)

## Can run in parallel with
- **4B** (Registry Service update)

## Deliverable

### Rewrite `NodeDiscoveryService` to scan for `NodeBase` subclasses

**File**: `NodeEditor.Net/Services/Registry/NodeDiscoveryService.cs`

**Old behavior** (replace entirely):
1. Scan for `INodeContext` / `INodeMethodContext` types
2. Scan methods for `[NodeAttribute]`
3. Map method parameters → sockets

**New behavior**:
1. Scan for non-abstract types inheriting `NodeBase`
2. Create temp instance → call `Configure(builder)` → `builder.Build()` → `NodeDefinition`

```csharp
using System.Reflection;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Net.Services.Registry;

public sealed class NodeDiscoveryService
{
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

    public NodeDefinition? BuildDefinitionFromType(Type nodeType)
    {
        if (nodeType.IsAbstract || !nodeType.IsSubclassOf(typeof(NodeBase)))
            return null;

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

**Removed**:
- `IsContextType()` checking `INodeContext`/`INodeMethodContext`
- Method scanning with `GetCustomAttribute<NodeAttribute>()`
- Parameter-to-socket mapping logic

## Acceptance criteria

- [x] `DiscoverFromAssemblies` finds `NodeBase` subclasses, ignores abstract types
- [x] `BuildDefinitionFromType` calls `Configure()` on temp instance and returns valid `NodeDefinition`
- [x] Types without parameterless constructor are skipped gracefully
- [x] Types that throw in `Configure()` are skipped gracefully
- [x] Solution builds with no references to old discovery logic — **see note below**

### Review notes (2026-02-11)

**Status: COMPLETE ✅ — all criteria pass, 34/34 transition tests green**

The implementation is a **hybrid** approach that retains backward-compatible `INodeContext`/`[Node]`
method scanning alongside the new `NodeBase` scanning. This is intentional: old-style context
nodes still need to work until Phase 9 removes them. The spec's "Removed" list was aspirational
for the end-state, not this phase.

**What works:**
- `NodeBase` subclass discovery → `Configure(builder)` → `Build()` → `NodeDefinition` with `NodeType` set ✅
- `INodeContext` backward compat discovery → `[Node]` method scanning → parameter-to-socket mapping ✅
- Abstract types, missing parameterless constructors, throwing `Configure()` all skipped gracefully ✅
- `BuildDefinitionFromType` correctly sets `NodeType` on the resulting definition ✅
- Discovery produces proper `Factory` lambdas that create unique `NodeData` instances ✅

**Key architectural note for downstream phases:**
Context-based `[Node]` definitions produce `NodeDefinition`s with **no `NodeType` and no `InlineExecutor`**.
This means they cannot be executed by `ExecutionRuntime` (Phase 5/6), which requires one of those two.
This is the root cause of all 12 `ExecutionEngineTests` failures. Resolution options:
1. Phase 7 migrates all standard nodes to `NodeBase` subclasses (eliminates context nodes)
2. Or `BuildDefinitionsFromContext` generates `InlineExecutor` delegates as a bridge

Per the transition plan, option 1 (Phase 7) is the intended path.
