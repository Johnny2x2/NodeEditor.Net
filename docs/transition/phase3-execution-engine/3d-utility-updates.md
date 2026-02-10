# 3D — Utility Updates (ExecutionGate, HeadlessGraphRunner, BackgroundExecution)

> **Parallelism**: Can run in parallel with **3C** once **3B** API is known.

## Prerequisites
- **3B** — need to know the `ExecutionRuntime` API shape
- **Phase 1** — `INodeRuntimeStorage` definition

## Can run in parallel with
- **3C** (Execution Service rewrite)

## Deliverables

### Update `ExecutionGate`

**File**: `NodeEditor.Net/Services/Execution/Runtime/ExecutionGate.cs`

**Minor update**: Gate is now checked inside `TriggerAsync()` (in `NodeExecutionContextImpl`) rather than in the step dispatcher. The gate API stays the same:
- `WaitAsync(ct)` — blocks until gate is open
- `Run()`, `Pause()`, `StepOnce()`, `Resume()`

No structural changes — just verify the call site moved from `ExecuteStepsAsync` to `NodeExecutionContextImpl.TriggerAsync`.

### Update `HeadlessGraphRunner`

**File**: `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs`

**Changes**:
- Remove `INodeContextFactory` / `CompositeNodeContext` creation
- Remove `nodeContext` parameter (no longer needed)
- Simplify constructor: remove `INodeContextFactory` dependency

```csharp
public sealed class HeadlessGraphRunner
{
    private readonly INodeExecutionService _executionService;
    private readonly IGraphSerializer _serializer;

    public HeadlessGraphRunner(
        INodeExecutionService executionService,
        IGraphSerializer serializer)
    {
        _executionService = executionService;
        _serializer = serializer;
    }

    public async Task ExecuteAsync(
        GraphData graphData,
        INodeRuntimeStorage? runtimeStorage = null,
        ExecutionOptions? options = null,
        CancellationToken ct = default)
    {
        runtimeStorage ??= new NodeRuntimeStorage();
        var nodes = graphData.Nodes;
        var connections = graphData.Connections;
        VariableNodeExecutor.SeedVariables(nodes, connections, runtimeStorage, graphData.Variables);
        await _executionService.ExecuteAsync(nodes, connections, runtimeStorage, options, ct);
    }

    public async Task ExecuteFromJsonAsync(string json, CancellationToken ct = default)
    {
        var graphData = _serializer.Deserialize(json);
        await ExecuteAsync(graphData, ct: ct);
    }
}
```

### `BackgroundExecutionQueue` and `BackgroundExecutionWorker`

**Files**:
- `NodeEditor.Net/Services/Execution/Runtime/BackgroundExecutionQueue.cs`
- `NodeEditor.Net/Services/Execution/Runtime/BackgroundExecutionWorker.cs`

**Likely no changes**: These wrap `INodeExecutionService.ExecuteAsync()`, whose public API is unchanged. Verify and update if `ExecutionOptions` shape changed.

## Acceptance criteria

- [ ] `ExecutionGate.WaitAsync` still works (called from `TriggerAsync` now)
- [ ] `HeadlessGraphRunner` constructor no longer requires `INodeContextFactory`
- [ ] `HeadlessGraphRunner.ExecuteAsync` works without `CompositeNodeContext`
- [ ] `BackgroundExecutionQueue/Worker` compile without changes (or minimal updates)
- [ ] Solution builds clean
