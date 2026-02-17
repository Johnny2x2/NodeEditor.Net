# 12C — Plugin Tests

> **Phase 12 — Independent Tests**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phase 10** complete (plugins rewritten)

## Can run in parallel with
- All other Phase 12 sub-tasks

## Deliverables

### Update `PluginLoaderTests.cs`

- Remove assertions checking `INodeContextRegistry` was populated
- Verify `NodeBase` subclasses from plugin assemblies are discovered
- Keep: lifecycle tests (load, unload, register)

### Update `DynamicPluginLoadingTests.cs`

- Plugins now expose `NodeBase` subclasses instead of `INodeContext` types
- Verify: `registry.Definitions` contains definitions from plugin `NodeBase` subclasses after loading
- Verify: definitions removed after unloading

### Keep unchanged

- `PluginLifecycleTests.cs` — minor: remove any `INodeContext` assertions
- `PluginEventBusTests.cs` — no changes
- `PluginServiceRegistryTests.cs` — no changes

## Acceptance criteria

- [ ] No references to `INodeContextRegistry` in plugin tests
- [ ] Plugin load → `NodeBase` subclasses discovered and registered
- [ ] Plugin unload → definitions removed
- [ ] Lifecycle hooks still tested
