# 11A — DI Registration Updates

> **Phase 11 — Blazor Integration**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phase 9** complete (old types deleted — DI must not register them)

## Can run in parallel with
- All other Phase 11 sub-tasks

## Deliverable

### Update `NodeEditorServiceExtensions`

**File**: `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs`

**Remove registrations**:
```diff
- services.AddSingleton<INodeContextFactory, NodeContextFactory>();
- services.AddSingleton<INodeContextRegistry, NodeContextRegistry>();
```

**Keep all other registrations** unchanged:
- `NodeDiscoveryService` (Singleton, rewritten)
- `INodeRegistryService` → `NodeRegistryService` (Singleton)
- `ISocketTypeResolver` → `SocketTypeResolver` (Singleton, updated in 11E)
- `ExecutionPlanner` (Singleton, simplified)
- `INodeExecutionService` → `NodeExecutionService` (Scoped, rewritten)
- `IPluginLoader` → `PluginLoader` (Singleton, updated in 10A)
- `IPluginServiceRegistry`, `VariableNodeFactory`, `EventNodeFactory`, etc.

## Acceptance criteria

- [ ] `INodeContextFactory` and `INodeContextRegistry` not registered
- [ ] All other DI registrations compile and resolve correctly
- [ ] Application starts without DI resolution errors
