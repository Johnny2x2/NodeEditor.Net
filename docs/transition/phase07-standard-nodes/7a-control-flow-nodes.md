# 7A — Control Flow Nodes (Start, Branch)

> **Phase 7 — Standard Nodes**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–6** complete (`NodeBase`, `INodeBuilder`, `INodeExecutionContext`, execution engine)

## Can run in parallel with
- **7B**, **7C**, **7D**, **7E**, **7F**, **7G**

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
