# 8C — Streaming Tests

> **Parallelism**: Depends on **8A**. Can run in parallel with **8B**, **8D**, **8E**, **8F**, **8G**.

## Prerequisites
- **8A** (Test Infrastructure — `TestStreamingNode`, `TestCollectorNode`)
- **All implementation phases** (1–7) complete

## Can run in parallel with
- **8B**, **8D**, **8E**, **8F**, **8G**

## Deliverable

### New file: `StreamingExecutionTests.cs`

**File**: `NodeEditor.Blazor.Tests/StreamingExecutionTests.cs`

**Test cases**:

| Test | What it verifies |
|------|------------------|
| `EmitAsync_Sequential_WaitsForDownstream` | Sequential mode blocks on each item |
| `EmitAsync_Sequential_ItemsReceivedInOrder` | Items arrive in emission order |
| `EmitAsync_FireAndForget_DoesNotWaitForDownstream` | Fire-and-forget runs concurrently |
| `CompletedPath_FiresAfterAllItems` | Completed path fires once, after all OnItem |
| `CompletedPath_DownstreamHasAccessToFinalState` | Final accumulated value accessible |
| `EmitAsync_DownstreamError_PropagatesInSequential` | Error propagation in sequential mode |
| `EmitAsync_Cancellation_StopsStreaming` | CancellationToken stops emission loop |

## Acceptance criteria

- [ ] All 7 streaming test cases pass
- [ ] Sequential mode timing verified (total >= items * downstream time)
- [ ] Fire-and-forget timing verified (total < items * downstream time)
- [ ] Completed path fires exactly once, after all OnItem triggers
- [ ] Error propagation works in sequential mode
- [ ] Cancellation interrupts streaming
