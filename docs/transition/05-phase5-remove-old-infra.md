# Phase 5 — Remove Old Infrastructure

> **Goal**: Delete all types, interfaces, and files that belong to the old attribute+method+reflection system. By this point, all standard nodes (Phase 4) and the execution engine (Phase 3) use the new system — these files are dead code.

## Files to delete

### 5.1 Node definition infrastructure

| File | Type | Why removed |
|------|------|-------------|
| `NodeEditor.Net/Services/Registry/INodeContext.cs` | Interface | Marker interface for old system. Replaced by `NodeBase`. |
| `NodeEditor.Net/Services/Execution/Nodes/NodeAttribute.cs` | Attribute | `[Node]` attribute for method discovery. Replaced by `NodeBase.Configure(INodeBuilder)`. |
| `NodeEditor.Net/Services/Execution/Nodes/SocketEditorAttribute.cs` | Attribute | `[SocketEditor]` on method parameters. Replaced by `NodeBuilder.Input<T>(..., editorHint)`. |

### 5.2 Execution dispatch infrastructure

| File | Type | Why removed |
|------|------|-------------|
| `NodeEditor.Net/Services/Execution/Runtime/NodeMethodInvoker.cs` | Class | Reflection-based method dispatch. Replaced by direct `NodeBase.ExecuteAsync()` calls. |
| `NodeEditor.Net/Services/Execution/Helpers/ExecutionPath.cs` | Class | Signalable flow control token. Replaced by `TriggerAsync()` on `INodeExecutionContext`. Flow is no longer signaled — it's directly called. |

### 5.3 Context aggregation infrastructure

| File | Type | Why removed |
|------|------|-------------|
| `NodeEditor.Net/Services/Execution/Context/INodeMethodContext.cs` | Interface | Feedback callback from shared context. Replaced by `INodeExecutionContext.EmitFeedback()`. |
| `NodeEditor.Net/Services/Execution/Context/INodeContextHost.cs` | Interface | Multi-context aggregation. Replaced by instance-per-node model. |
| `NodeEditor.Net/Services/Execution/Context/CompositeNodeContext.cs` | Class | Aggregates multiple `INodeContext` objects for reflection dispatch. No longer needed. |
| `NodeEditor.Net/Services/Execution/Context/NodeContextFactory.cs` | Class + Interface | `INodeContextFactory` + `NodeContextFactory`. Assembly scanning for `INodeContext` types. Discovery now scans for `NodeBase` subclasses. |
| `NodeEditor.Net/Services/Execution/Context/NodeContextRegistry.cs` | Class + Interface | `INodeContextRegistry` + `NodeContextRegistry`. Plugin context registration. Plugins now register `NodeBase` subclasses via assembly scanning. |

### 5.4 Old standard node files

| File | Why removed |
|------|-------------|
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.cs` | Main partial class with `_state` dictionary |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.Conditions.cs` | Branch + loop methods |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.Helpers.cs` | Start, Marker, Consume, Delay methods |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.DebugPrint.cs` | Debug Print, Warning, Error methods |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.Numbers.cs` | Math methods |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.Strings.cs` | String methods |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.Lists.cs` | List methods |
| `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeContext.Parallel.cs` | Empty placeholder |

---

## References to clean up

After deleting the files above, grep for remaining references and fix them:

### `INodeContext` references to remove

| File | Location | Action |
|------|----------|--------|
| `NodeEditor.Net/Services/Execution/Context/NodeContextFactory.cs` | `typeof(INodeContext).IsAssignableFrom(type)` | File deleted |
| `NodeEditor.Net/Services/Execution/Context/NodeContextRegistry.cs` | Docs reference | File deleted |
| `NodeEditor.Net/Services/Plugins/PluginLoader.cs` | `IsContextType()` scanning for `INodeContext` | Rewrite to scan for `NodeBase` (Phase 6) |
| Plugin projects | `class TestAPluginContext : INodeContext` | Remove interface (Phase 6) |

### `NodeAttribute` references to remove

| File | Location | Action |
|------|----------|--------|
| `NodeEditor.Net/Services/Registry/NodeDiscoveryService.cs` | `GetCustomAttribute<NodeAttribute>()` | Already rewritten (Phase 2) |
| `NodeEditor.Net/Services/Execution/Runtime/NodeMethodInvoker.cs` | Method map building | File deleted |
| All `StandardNodeContext.*.cs` files | `[Node(...)]` decorations | Files deleted |
| Plugin context classes | `[Node(...)]` decorations | Rewritten (Phase 6) |

### `ExecutionPath` references to remove

| File | Location | Action |
|------|----------|--------|
| `NodeEditor.Net/Services/Core/VariableNodeFactory.cs` | `typeof(ExecutionPath).FullName!` | Update to `ExecutionSocket.TypeName` |
| `NodeEditor.Net/Services/Core/EventNodeFactory.cs` | `typeof(ExecutionPath).FullName!` | Update to `ExecutionSocket.TypeName` |
| `NodeEditor.Net/Services/Execution/Runtime/NodeMethodInvoker.cs` | `ExecutionPath` creation/signaling | File deleted |
| `NodeEditor.Net/Services/Core/SocketTypeResolver.cs` | Pre-registers `ExecutionPath` type | Update to register `ExecutionSocket` marker |
| `NodeEditor.Net/Models/SocketData.cs` (conceptual) | `IsExecution` check uses type name | Update type name constant |

### `INodeMethodContext` / `CompositeNodeContext` references to remove

| File | Location | Action |
|------|----------|--------|
| `NodeEditor.Net/Services/Execution/Runtime/NodeExecutionService.cs` | Creates `CompositeNodeContext`, subscribes to `FeedbackInfo` | Already rewritten (Phase 3) |
| `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs` | Creates `CompositeNodeContext` | Updated (Phase 3) |
| `NodeEditor.Net/Services/Plugins/PluginLoader.cs` | Registers contexts in `INodeContextRegistry` | Rewrite (Phase 6) |

---

## Update `VariableNodeFactory` and `EventNodeFactory`

These files build `NodeDefinition`s programmatically. They reference `ExecutionPath` type name. Update to use `ExecutionSocket.TypeName`:

### `VariableNodeFactory.cs` — changes

```diff
- new SocketData("Enter", typeof(ExecutionPath).FullName!, IsInput: true, IsExecution: true),
+ new SocketData("Enter", ExecutionSocket.TypeName, IsInput: true, IsExecution: true),

- new SocketData("Exit", typeof(ExecutionPath).FullName!, IsInput: false, IsExecution: true),
+ new SocketData("Exit", ExecutionSocket.TypeName, IsInput: false, IsExecution: true),
```

### `EventNodeFactory.cs` — same pattern

```diff
- var execType = typeof(ExecutionPath).FullName!;
+ var execType = ExecutionSocket.TypeName;
```

---

## Update `SocketTypeResolver`

The `SocketTypeResolver` currently pre-registers `ExecutionPath`:

```diff
- Register(typeof(ExecutionPath));
+ // ExecutionSocket is a static marker class, not instantiable.
+ // Execution sockets are identified by TypeName string, not by runtime Type.
```

---

## Rename `INodeExecutionContext` (old) → `INodeRuntimeStorage`

The old `INodeExecutionContext` is renamed to `INodeRuntimeStorage` (Phase 1 creates the new interface). The existing implementation `NodeExecutionContext` becomes `NodeRuntimeStorage`:

| Old name | New name | File |
|----------|----------|------|
| `INodeExecutionContext` (old) | `INodeRuntimeStorage` | `Context/INodeRuntimeStorage.cs` (new) |
| `NodeExecutionContext` (old) | `NodeRuntimeStorage` | `Context/NodeExecutionContext.cs` → rename |

Update all internal engine references from `INodeExecutionContext` → `INodeRuntimeStorage`. External consumers (`NodeBase` subclasses) only see the new `INodeExecutionContext` (high-level API).

---

## Summary cleanup checklist

- [ ] Delete 5 infrastructure files (INodeContext, NodeAttribute, SocketEditorAttribute, NodeMethodInvoker, ExecutionPath)
- [ ] Delete 5 context files (INodeMethodContext, INodeContextHost, CompositeNodeContext, NodeContextFactory, NodeContextRegistry)
- [ ] Delete 8 old StandardNodeContext files
- [ ] Update VariableNodeFactory: `ExecutionPath` → `ExecutionSocket.TypeName`
- [ ] Update EventNodeFactory: `ExecutionPath` → `ExecutionSocket.TypeName`
- [ ] Update SocketTypeResolver: remove `ExecutionPath` registration
- [ ] Rename `NodeExecutionContext` → `NodeRuntimeStorage` (implements `INodeRuntimeStorage`)
- [ ] Grep for any remaining references to deleted types, fix all

## Dependencies

- Depends on Phase 3 (execution engine no longer uses old types)
- Depends on Phase 4 (all standard nodes migrated)
- Phase 6 (plugins) and Phase 7 (Blazor) should be done in parallel or after
