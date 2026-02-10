# 4B — NodeRegistryService Update

> **Phase 4 — Discovery & Registry Services**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–3** complete (NodeDefinition extended, builder API exists)

## Can run in parallel with
- **4A** (Discovery Service rewrite)

## Deliverable

### Update `NodeRegistryService` internal discovery delegation

**File**: `NodeEditor.Net/Services/Registry/NodeRegistryService.cs`

The registry's public API stays the same. Internal changes:

```csharp
public void RegisterFromAssembly(Assembly assembly)
{
    var newDefinitions = _discoveryService.DiscoverFromAssemblies(new[] { assembly });
    RegisterDefinitions(newDefinitions);
}
```

- `EnsureInitialized(assemblies)` → same pattern, discovers from all listed assemblies using the rewritten discovery service
- `RegisterPluginAssembly(assembly)` → same as `RegisterFromAssembly` but tracks as plugin definitions

### Add inline definition registration call

After assembly-based discovery, also register inline data nodes:

```csharp
public void EnsureInitialized(IEnumerable<Assembly> assemblies)
{
    // Discover class-based nodes
    var definitions = _discoveryService.DiscoverFromAssemblies(assemblies);
    RegisterDefinitions(definitions);

    // Register inline lambda data nodes
    RegisterDefinitions(StandardNodeRegistration.GetInlineDefinitions());
}
```

### No changes to these

| Type | Why unchanged |
|------|---------------|
| `INodeRegistryService` interface | Already generic — works with `NodeDefinition` |
| `NodeCatalog` | Groups by `Category`, filters by name — no coupling to execution |
| `RegisterDefinitions()` | Same logic — deduplicates by `Id` |
| `RemoveDefinitions()` | Same logic — removes by reference |
| `GetCatalog()` | Unchanged |

## Acceptance criteria

- [ ] `RegisterFromAssembly` delegates to rewritten `NodeDiscoveryService`
- [ ] `EnsureInitialized` also calls `StandardNodeRegistration.GetInlineDefinitions()`
- [ ] All existing public API methods compile unchanged
- [ ] `INodeRegistryService` interface has no breaking changes
