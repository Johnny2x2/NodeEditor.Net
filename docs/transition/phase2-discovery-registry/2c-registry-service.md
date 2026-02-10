# 2C — NodeRegistryService Update

> **Parallelism**: Can run in parallel with **2B** after **2A** completes.

## Prerequisites
- **2A** — NodeDefinition extended (the registry stores `NodeDefinition` records)
- Phase 1 complete (builder API exists)

## Can run in parallel with
- **2B** (Discovery Service rewrite)

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
