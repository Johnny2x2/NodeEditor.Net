# 4E — Number Inline Nodes

> **Parallelism**: Can run in parallel with **4A**, **4B**, **4C**, **4D**, **4F**, **4G**.

## Prerequisites
- **Phase 1** complete (`NodeBuilder`, `INodeExecutionContext`)

## Can run in parallel with
- All other Phase 4 sub-tasks

## Deliverable

### `StandardNumberNodes` — 10 inline lambda definitions

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/StandardNumberNodes.cs`

```csharp
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardNumberNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("Abs")
            .Category("Numbers").Description("Absolute value.")
            .Input<double>("Value").Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Abs(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Min")
            .Category("Numbers").Description("Minimum of two values.")
            .Input<double>("A").Input<double>("B").Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Min(ctx.GetInput<double>("A"), ctx.GetInput<double>("B"))))
            .Build();

        yield return NodeBuilder.Create("Max")
            .Category("Numbers").Description("Maximum of two values.")
            .Input<double>("A").Input<double>("B").Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Max(ctx.GetInput<double>("A"), ctx.GetInput<double>("B"))))
            .Build();

        yield return NodeBuilder.Create("Mod")
            .Category("Numbers").Description("Modulus (remainder).")
            .Input<double>("A").Input<double>("B", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") % ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Round")
            .Category("Numbers").Description("Rounds to nearest integer.")
            .Input<double>("Value").Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Round(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Floor")
            .Category("Numbers").Description("Rounds down.")
            .Input<double>("Value").Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Floor(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Ceiling")
            .Category("Numbers").Description("Rounds up.")
            .Input<double>("Value").Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Ceiling(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Clamp")
            .Category("Numbers").Description("Clamps value between min and max.")
            .Input<double>("Value").Input<double>("Min", 0.0).Input<double>("Max", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Clamp(ctx.GetInput<double>("Value"), ctx.GetInput<double>("Min"), ctx.GetInput<double>("Max"))))
            .Build();

        yield return NodeBuilder.Create("Random Range")
            .Category("Numbers").Description("Random integer in range.")
            .Input<int>("Min", 0).Input<int>("Max", 100).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Random.Shared.Next(ctx.GetInput<int>("Min"), ctx.GetInput<int>("Max"))))
            .Build();

        yield return NodeBuilder.Create("Sign")
            .Category("Numbers").Description("Returns the sign of a value (-1, 0, or 1).")
            .Input<double>("Value").Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Sign(ctx.GetInput<double>("Value"))))
            .Build();
    }
}
```

## Acceptance criteria

- [ ] `GetDefinitions()` returns 10 `NodeDefinition` instances
- [ ] Each has `InlineExecutor != null` and `NodeType == null`
- [ ] All nodes in "Numbers" category
- [ ] Abs(-5) = 5, Min(3,7) = 3, Max(3,7) = 7, Mod(7,3) = 1, Round(3.6) = 4, Floor(3.9) = 3, Ceiling(3.1) = 4, Clamp(15,0,10) = 10, Sign(-5) = -1
