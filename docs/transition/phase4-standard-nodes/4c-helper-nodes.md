# 4C — Helper Nodes (Marker, Consume, Delay)

> **Parallelism**: Can run in parallel with **4A**, **4B**, **4D**, **4E**, **4F**, **4G**.

## Prerequisites
- **Phase 1** complete (`NodeBase`, `INodeBuilder`, `INodeExecutionContext`)

## Can run in parallel with
- All other Phase 4 sub-tasks

## Deliverables

### `MarkerNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/MarkerNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class MarkerNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Marker").Category("Helpers")
            .Description("Passthrough marker for organizing flows.")
            .Callable();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
```

### `ConsumeNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/ConsumeNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ConsumeNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Consume").Category("Helpers")
            .Description("Consumes a value (forces upstream evaluation) and continues.")
            .Callable()
            .Input<object>("Value");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        _ = context.GetInput<object>("Value");
        await context.TriggerAsync("Exit");
    }
}
```

### `DelayNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/DelayNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DelayNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Delay").Category("Helpers")
            .Description("Pauses execution for a specified time.")
            .Callable()
            .Input<int>("DelayMs", 1000);
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var delayMs = context.GetInput<int>("DelayMs");
        await Task.Delay(delayMs, ct);
        await context.TriggerAsync("Exit");
    }
}
```

## Acceptance criteria

- [ ] Marker is a passthrough — Enter triggers Exit with no side effects
- [ ] Consume forces upstream lazy evaluation of its "Value" input
- [ ] Delay pauses for the specified milliseconds, respects cancellation
- [ ] All 3 nodes discoverable and compile clean
