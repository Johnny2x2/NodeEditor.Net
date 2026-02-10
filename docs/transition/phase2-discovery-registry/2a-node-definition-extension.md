# 2A — NodeDefinition Record Extension

> **Parallelism**: No sub-phase dependencies. Prerequisite for **2B** and **2C**.

## Prerequisites
- **Phase 1** complete (needs `StreamSocketInfo` from 1A, `INodeExecutionContext` from 1B)

## Can run in parallel with
- Nothing within Phase 2 (this is the first step). Quick change — likely < 30 minutes.

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

- [ ] `NodeDefinition` record compiles with all 10 fields
- [ ] Existing code creating `NodeDefinition` with 7 args still compiles unchanged
- [ ] New fields accessible: `def.NodeType`, `def.InlineExecutor`, `def.StreamSockets`
- [ ] Solution builds clean
