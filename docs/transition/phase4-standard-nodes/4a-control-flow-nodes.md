# 4A — Control Flow Nodes (Start, Branch)

> **Parallelism**: Can run in parallel with **4B**, **4C**, **4D**, **4E**, **4F**, **4G**.

## Prerequisites
- **Phase 1** complete (`NodeBase`, `INodeBuilder`, `INodeExecutionContext`)
- **Phase 3** complete (execution engine supports `TriggerAsync`)

## Can run in parallel with
- All other Phase 4 sub-tasks (4B–4G)

## Deliverables

### `StartNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/StartNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class StartNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Start")
            .Category("Helpers")
            .Description("Entry point for execution. Place one to begin a flow.")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
```

### `BranchNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/BranchNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class BranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Branch")
            .Category("Conditions")
            .Description("Branch execution on a boolean condition.")
            .ExecutionInput("Start")
            .Input<bool>("Cond")
            .ExecutionOutput("True")
            .ExecutionOutput("False");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var cond = context.GetInput<bool>("Cond");
        await context.TriggerAsync(cond ? "True" : "False");
    }
}
```

## Acceptance criteria

- [ ] `StartNode` discovered as `ExecutionInitiator` with Exit socket only (no Enter)
- [ ] `BranchNode` has Start (exec input), Cond (bool input), True (exec output), False (exec output)
- [ ] `StartNode.ExecuteAsync` triggers "Exit"
- [ ] `BranchNode.ExecuteAsync` triggers "True" or "False" based on `Cond` input
- [ ] Both classes compile and are discoverable by the new `NodeDiscoveryService`
