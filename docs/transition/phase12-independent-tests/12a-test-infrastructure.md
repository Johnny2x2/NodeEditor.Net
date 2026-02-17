# 12A — Test Infrastructure (Helpers, Builders, Test Nodes)

> **Phase 12 — Independent Tests**
> All sub-tasks in this phase can run fully in parallel. Prerequisite for **Phase 13**.

## Prerequisites
- **Phase 2** complete (`NodeBase`, `NodeBuilder`, `INodeExecutionContext`)

## Can run in parallel with
- **12B**, **12C**, **12D**, **12E**

## Deliverables

### `TestStreamingNode`

```csharp
public sealed class TestStreamingNode : NodeBase
{
    private readonly int _itemCount;
    private readonly int _delayPerItemMs;

    public TestStreamingNode(int itemCount = 3, int delayPerItemMs = 0)
    {
        _itemCount = itemCount;
        _delayPerItemMs = delayPerItemMs;
    }

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Test Stream").Category("Test").Callable()
            .StreamOutput<string>("Item", "OnItem", "Completed");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        for (int i = 0; i < _itemCount; i++)
        {
            if (_delayPerItemMs > 0) await Task.Delay(_delayPerItemMs, ct);
            await context.EmitAsync("Item", $"item-{i}");
        }
        await context.TriggerAsync("Completed");
    }
}
```

### `TestCollectorNode`

```csharp
public sealed class TestCollectorNode : NodeBase
{
    public List<object?> Collected { get; } = new();

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Collector").Category("Test").Callable().Input<object>("Value");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        Collected.Add(context.GetInput<object>("Value"));
        return context.TriggerAsync("Exit");
    }
}
```

### `TestGraphBuilder` — fluent graph construction for tests

```csharp
public sealed class TestGraphBuilder
{
    private readonly List<NodeData> _nodes = new();
    private readonly List<ConnectionData> _connections = new();

    public TestGraphBuilder AddNode(NodeBase node, out string nodeId) { /* ... */ }
    public TestGraphBuilder ConnectExecution(string from, string fromSocket, string to, string toSocket) { /* ... */ }
    public TestGraphBuilder ConnectData(string from, string fromSocket, string to, string toSocket) { /* ... */ }
    public (IReadOnlyList<NodeData>, IReadOnlyList<ConnectionData>) Build() => (...);
}
```

## Acceptance criteria

- [ ] `TestStreamingNode` emits configurable number of items with optional delay
- [ ] `TestCollectorNode` captures all received items for assertion
- [ ] `TestGraphBuilder` produces valid `NodeData` + `ConnectionData` collections
- [ ] All helpers compile in the test project
