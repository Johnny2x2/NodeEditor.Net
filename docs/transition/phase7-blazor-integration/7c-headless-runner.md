# 7C — HeadlessGraphRunner Simplification

> **Parallelism**: Can run in parallel with **7A**, **7B**, **7D**, **7E**.

## Prerequisites
- **Phase 3** complete (execution service rewritten)
- **Phase 5** complete (`CompositeNodeContext` removed)

## Can run in parallel with
- All other Phase 7 sub-tasks

## Deliverable

### Simplify `HeadlessGraphRunner`

**File**: `NodeEditor.Net/Services/Execution/Runtime/HeadlessGraphRunner.cs`

Remove `INodeContextFactory` dependency and `CompositeNodeContext` creation. Node instances are now created per-node by the `ExecutionRuntime`.

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
        await _executionService.ExecuteAsync(
            graphData.Nodes, graphData.Connections, runtimeStorage, options, ct);
    }

    public async Task ExecuteFromJsonAsync(string json, CancellationToken ct = default)
    {
        var graphData = _serializer.Deserialize(json);
        await ExecuteAsync(graphData, ct: ct);
    }
}
```

## Acceptance criteria

- [ ] No `INodeContextFactory` in constructor
- [ ] No `CompositeNodeContext` creation
- [ ] `ExecuteAsync` delegates directly to `_executionService`
- [ ] `ExecuteFromJsonAsync` still works end-to-end
