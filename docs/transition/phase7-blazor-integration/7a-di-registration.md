# 7A — DI Registration Updates

> **Parallelism**: Can run in parallel with **7B**, **7C**, **7D**, **7E**.

## Prerequisites
- **Phase 5** complete (old types deleted — DI must not register them)

## Can run in parallel with
- All other Phase 7 sub-tasks

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
- `ISocketTypeResolver` → `SocketTypeResolver` (Singleton, updated in 7E)
- `ExecutionPlanner` (Singleton, simplified)
- `INodeExecutionService` → `NodeExecutionService` (Scoped, rewritten)
- `IPluginLoader` → `PluginLoader` (Singleton, updated in 6A)
- `IPluginServiceRegistry`, `VariableNodeFactory`, `EventNodeFactory`, etc.

## Acceptance criteria

- [ ] `INodeContextFactory` and `INodeContextRegistry` not registered
- [ ] All other DI registrations compile and resolve correctly
- [ ] Application starts without DI resolution errors
