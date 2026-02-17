# 9B — Update Factories (VariableNodeFactory, EventNodeFactory)

> **Phase 9 — Remove Old Infrastructure**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–8** complete (`ExecutionSocket.TypeName` exists)

## Can run in parallel with
- **9A** (Delete Old Files), **9C** (Reference Cleanup)

## Deliverables

### `VariableNodeFactory.cs`

**File**: `NodeEditor.Net/Services/Core/VariableNodeFactory.cs`

Replace all references to `typeof(ExecutionPath).FullName!` with `ExecutionSocket.TypeName`:

```diff
- new SocketData("Enter", typeof(ExecutionPath).FullName!, IsInput: true, IsExecution: true),
+ new SocketData("Enter", ExecutionSocket.TypeName, IsInput: true, IsExecution: true),

- new SocketData("Exit", typeof(ExecutionPath).FullName!, IsInput: false, IsExecution: true),
+ new SocketData("Exit", ExecutionSocket.TypeName, IsInput: false, IsExecution: true),
```

Add `using NodeEditor.Net.Services.Execution;` if not present.

### `EventNodeFactory.cs`

**File**: `NodeEditor.Net/Services/Core/EventNodeFactory.cs`

Same pattern:

```diff
- var execType = typeof(ExecutionPath).FullName!;
+ var execType = ExecutionSocket.TypeName;
```

Remove `using` for `ExecutionPath` namespace if no longer needed.

## Acceptance criteria

- [ ] No references to `ExecutionPath` remain in either factory
- [ ] Both factories use `ExecutionSocket.TypeName` for execution socket type names
- [ ] Factories still produce valid `NodeDefinition` records
- [ ] Variable get/set nodes and Event trigger/listener nodes still work at runtime
