# 1A — Utility Types (ExecutionSocket, StreamMode, StreamSocketInfo)

> **Phase 1 — Core Types & Interfaces**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- None — these are standalone types.

## Can run in parallel with
- **1B** (Core Interfaces)

## Deliverables

### `ExecutionSocket` — Marker type replacing `ExecutionPath`

**File**: `NodeEditor.Net/Services/Execution/Helpers/ExecutionSocket.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Marker type for execution sockets. Unlike the old ExecutionPath (which carried
/// IsSignaled state), this is purely a type marker used in SocketData.TypeName
/// to distinguish execution sockets from data sockets. Flow control is handled
/// by TriggerAsync() on INodeExecutionContext, not by signaling objects.
/// </summary>
public static class ExecutionSocket
{
    public static readonly string TypeName = "NodeEditor.Net.Services.Execution.ExecutionSocket";
}
```

### `StreamMode` enum

**File**: `NodeEditor.Net/Services/Execution/Nodes/StreamMode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Controls how streaming emissions interact with downstream execution.
/// </summary>
public enum StreamMode
{
    /// <summary>
    /// Each EmitAsync call waits for downstream nodes to complete before returning.
    /// The node processes items sequentially.
    /// </summary>
    Sequential,

    /// <summary>
    /// EmitAsync returns immediately. Downstream nodes run concurrently.
    /// The node continues producing items without waiting.
    /// </summary>
    FireAndForget
}
```

### `StreamSocketInfo` record

**File**: `NodeEditor.Net/Services/Execution/Nodes/StreamSocketInfo.cs`

```csharp
namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Metadata about a streaming socket group declared via StreamOutput().
/// </summary>
public sealed record StreamSocketInfo(
    string ItemDataSocket,
    string OnItemExecSocket,
    string? CompletedExecSocket);
```

## Acceptance criteria

- [ ] `ExecutionSocket.TypeName` returns `"NodeEditor.Net.Services.Execution.ExecutionSocket"`
- [ ] `StreamMode` enum has `Sequential` and `FireAndForget` members
- [ ] `StreamSocketInfo` record compiles and holds 3 fields
- [ ] Solution builds clean with new files added
