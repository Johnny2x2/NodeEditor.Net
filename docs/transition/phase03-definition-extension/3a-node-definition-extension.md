# 3A — NodeDefinition Record Extension

> **Phase 3 — Definition Extension**
> Single sub-task phase. Quick change — likely < 30 minutes.

## Prerequisites
- **Phase 2** complete (needs `StreamSocketInfo` from Phase 1/1A, `INodeExecutionContext` from Phase 1/1B, `NodeBuilder` from Phase 2)

## Can run in parallel with
- Nothing (sole sub-task in this phase). Unlocks Phase 4.

## Deliverable

### Extend `NodeDefinition` with 3 optional fields

**File**: `NodeEditor.Net/Services/Registry/NodeDefinition.cs`

**Current** (7 positional parameters):
```csharp
public sealed record class NodeDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    Func<NodeData> Factory);
```

**New** (add 3 optional parameters with defaults):
```csharp
public sealed record class NodeDefinition(
    string Id,
    string Name,
    string Category,
    string Description,
    IReadOnlyList<SocketData> Inputs,
    IReadOnlyList<SocketData> Outputs,
    Func<NodeData> Factory,
    Type? NodeType = null,
    Func<INodeExecutionContext, CancellationToken, Task>? InlineExecutor = null,
    IReadOnlyList<StreamSocketInfo>? StreamSockets = null);
```

| New field | Purpose |
|-----------|---------|
| `NodeType` | The `NodeBase` subclass `Type`. Used by the engine to create instances. `null` for inline/lambda nodes. |
| `InlineExecutor` | For `NodeBuilder.Create().OnExecute(lambda)` nodes. `null` for class-based nodes. |
| `StreamSockets` | Streaming socket group metadata. Used by the engine to route `EmitAsync()`. |

**Backward-compatible**: All existing callers passing 7 positional args still compile since new params have defaults.

## Acceptance criteria

- [x] `NodeDefinition` record compiles with all 10 fields
- [x] Existing code creating `NodeDefinition` with 7 args still compiles unchanged
- [x] New fields accessible: `def.NodeType`, `def.InlineExecutor`, `def.StreamSockets`
- [x] Solution builds clean
