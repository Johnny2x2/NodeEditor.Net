# 5C — Reference Cleanup & Renames

> **Parallelism**: Can run in parallel with **5A** and **5B**.

## Prerequisites
- **Phase 1** complete (`INodeRuntimeStorage` defined)
- **Phase 3** complete (execution engine uses new types)

## Can run in parallel with
- **5A** (Delete Old Files), **5B** (Update Factories)

## Deliverables

### Update `SocketTypeResolver`

**File**: `NodeEditor.Net/Services/Core/SocketTypeResolver.cs`

```diff
  public SocketTypeResolver()
  {
-     Register(typeof(ExecutionPath));
      Register(typeof(SerializableList));
      // ... other type registrations unchanged
  }
```

Execution sockets are identified by `SocketData.IsExecution == true`, not by type resolution.

### Rename `NodeExecutionContext` → `NodeRuntimeStorage`

The old `INodeExecutionContext` implementation class becomes `INodeRuntimeStorage` implementation:

| Old | New | File |
|-----|-----|------|
| `INodeExecutionContext` (old interface) | `INodeRuntimeStorage` | New file from 1B |
| `NodeExecutionContext` (old class) | `NodeRuntimeStorage` | Rename class + file |

Update all internal engine references:
- `NodeExecutionService` (already rewritten in Phase 3)
- `HeadlessGraphRunner` (already updated in 3D)
- `NodeEditorState.ApplyExecutionContext()` → `ApplyExecutionContext(INodeRuntimeStorage)`

### Grep & fix remaining references

After 5A deletes files and 5B/5C update the main references, run:

```bash
grep -rn "INodeContext\b" NodeEditor.Net/ --include="*.cs"
grep -rn "NodeAttribute\b" NodeEditor.Net/ --include="*.cs"
grep -rn "ExecutionPath\b" NodeEditor.Net/ --include="*.cs"
grep -rn "INodeMethodContext\b" NodeEditor.Net/ --include="*.cs"
grep -rn "CompositeNodeContext\b" NodeEditor.Net/ --include="*.cs"
grep -rn "NodeContextFactory\b" NodeEditor.Net/ --include="*.cs"
grep -rn "NodeContextRegistry\b" NodeEditor.Net/ --include="*.cs"
```

Fix any remaining hits (should be in comments, docs, or plugin code handled in Phase 6).

## Acceptance criteria

- [ ] `SocketTypeResolver` no longer registers `ExecutionPath`
- [ ] `NodeExecutionContext` renamed to `NodeRuntimeStorage` implementing `INodeRuntimeStorage`
- [ ] `NodeEditorState.ApplyExecutionContext` takes `INodeRuntimeStorage`
- [ ] Grep for old type names returns zero hits in `NodeEditor.Net/` (excluding docs)
- [ ] `dotnet build NodeEditor.Net/NodeEditor.Net.csproj` compiles clean (with 5A + 5B applied)
