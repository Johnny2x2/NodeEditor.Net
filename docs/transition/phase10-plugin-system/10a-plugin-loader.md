# 10A — PluginLoader Updates

> **Phase 10 — Plugin System**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phase 9** complete (old types deleted)

## Can run in parallel with
- **10B** (Template Plugin), **10C** (TestA Plugin), **10D** (TestB Plugin)

## Deliverable

### Rewrite `PluginLoader`

**File**: `NodeEditor.Net/Services/Plugins/PluginLoader.cs`

**Remove**:
- `RegisterNodeContextsFromAssembly()` method
- All references to `INodeContextRegistry`
- Cross-assembly reflection hacks for `NodeAttribute` name checking

**Update constructor**:
```diff
  public PluginLoader(
      INodeRegistryService registry,
-     INodeContextRegistry contextRegistry,
      IPluginServiceRegistry serviceRegistry,
      // ...
  )
```

**Update load flow** — `LoadAndRegisterAsync()`:
- `plugin.Register(registry)` — unchanged (plugin calls `registry.RegisterFromAssembly()`)
- If plugin is `INodeProvider`, register inline definitions — unchanged
- **Remove**: `RegisterNodeContextsFromAssembly(_contextRegistry, entry.Assembly)` call

**Update unload flow**:
- **Remove**: `_contextRegistry.Unregister()` calls
- Keep: `_registry.RemoveDefinitions()`, `_registry.RemoveDefinitionsFromAssembly()`
- Keep: plugin service cleanup, log channel cleanup, `entry.LoadContext.Unload()`

### Cross-assembly compatibility

The new system is simpler:
- Primary: `type.IsSubclassOf(typeof(NodeBase))` — works when plugin references `NodeEditor.Net.dll`
- Fallback: `type.BaseType.FullName == "NodeEditor.Net.Services.Execution.NodeBase"` — for isolated `AssemblyLoadContext` where type identity may differ

## Acceptance criteria

- [ ] `PluginLoader` constructor no longer takes `INodeContextRegistry`
- [ ] No references to `RegisterNodeContextsFromAssembly` remain
- [ ] Plugin load still calls `plugin.Register(registry)` which discovers `NodeBase` subclasses
- [ ] Plugin unload still removes definitions from registry
- [ ] Cross-assembly fallback works for isolated load contexts
