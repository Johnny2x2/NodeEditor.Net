# 7D — NodeEditorState Update

> **Parallelism**: Can run in parallel with **7A**, **7B**, **7C**, **7E**.

## Prerequisites
- **Phase 1** complete (`INodeRuntimeStorage` defined)
- **5C** complete (rename applied)

## Can run in parallel with
- All other Phase 7 sub-tasks

## Deliverable

### Update `NodeEditorState.ApplyExecutionContext`

**File**: `NodeEditor.Net/Services/Core/NodeEditorState.cs`

Rename parameter type from old `INodeExecutionContext` to `INodeRuntimeStorage`:

```diff
- public void ApplyExecutionContext(INodeExecutionContext context)
+ public void ApplyExecutionContext(INodeRuntimeStorage runtimeStorage)
```

The method body reads socket values from the runtime storage and maps them back to ViewModels — logic stays the same, just the parameter type name changes.

### `BuildExecutionNodes()` — No change

```csharp
public IReadOnlyList<NodeData> BuildExecutionNodes()
{
    return Nodes
        .Select(node => new NodeData(
            node.Data.Id, node.Data.Name, node.Data.Callable, node.Data.ExecInit,
            node.Inputs.Select(s => s.Data).ToList(),
            node.Outputs.Select(s => s.Data).ToList(),
            node.Data.DefinitionId))
        .ToList();
}
```

Unchanged — snapshots ViewModel state into immutable `NodeData` records.

## Acceptance criteria

- [ ] `ApplyExecutionContext` parameter is `INodeRuntimeStorage`
- [ ] Socket value mapping logic unchanged
- [ ] `BuildExecutionNodes()` unchanged
- [ ] UI still updates after execution completes
