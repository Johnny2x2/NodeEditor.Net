# 7B — Loop Nodes

> **Phase 7 — Standard Nodes**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–6** complete (`NodeBase`, `INodeBuilder`, `INodeExecutionContext`, execution engine)

## Can run in parallel with
- **7A**, **7C**, **7D**, **7E**, **7F**, **7G**

## Deliverables

### `ForLoopNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/ForLoopNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop").Category("Conditions")
            .Description("Iterates a fixed number of times.")
            .Callable()
            .Input<int>("LoopTimes", 10)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var loopTimes = context.GetInput<int>("LoopTimes");
        for (int i = 0; i < loopTimes && !ct.IsCancellationRequested; i++)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }
        await context.TriggerAsync("Exit");
    }
}
```

### `ForLoopStepNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/ForLoopStepNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForLoopStepNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop Step").Category("Conditions")
            .Description("Iterates from start to end with a configurable step.")
            .Callable()
            .Input<int>("StartValue", 0).Input<int>("EndValue", 10).Input<int>("Step", 1)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var start = context.GetInput<int>("StartValue");
        var end = context.GetInput<int>("EndValue");
        var step = context.GetInput<int>("Step");
        if (step == 0) step = 1;
        for (int i = start; (step > 0 ? i < end : i > end) && !ct.IsCancellationRequested; i += step)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }
        await context.TriggerAsync("Exit");
    }
}
```

### `ForEachLoopNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/ForEachLoopNode.cs`

```csharp
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForEachLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("ForEach Loop").Category("Conditions")
            .Description("Iterates over each item in a list.")
            .Callable()
            .Input<SerializableList>("List")
            .Output<object>("Obj")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var list = context.GetInput<SerializableList>("List");
        if (list?.Items is not null)
        {
            foreach (var item in list.Items)
            {
                ct.ThrowIfCancellationRequested();
                context.SetOutput("Obj", item);
                await context.TriggerAsync("LoopPath");
            }
        }
        await context.TriggerAsync("Exit");
    }
}
```

### `WhileLoopNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/WhileLoopNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class WhileLoopNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("While Loop").Category("Conditions")
            .Description("Loops while a condition is true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        while (context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested)
        {
            await context.TriggerAsync("LoopPath");
            iteration++;
        }
        await context.TriggerAsync("Exit");
    }
}
```

### `DoWhileLoopNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/DoWhileLoopNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DoWhileLoopNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Do While Loop").Category("Conditions")
            .Description("Executes body at least once, then loops while condition is true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        do {
            ct.ThrowIfCancellationRequested();
            await context.TriggerAsync("LoopPath");
            iteration++;
        } while (context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested);
        await context.TriggerAsync("Exit");
    }
}
```

### `RepeatUntilNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/RepeatUntilNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class RepeatUntilNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Repeat Until").Category("Conditions")
            .Description("Repeats body until condition becomes true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath").ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        do {
            ct.ThrowIfCancellationRequested();
            await context.TriggerAsync("LoopPath");
            iteration++;
        } while (!context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested);
        await context.TriggerAsync("Exit");
    }
}
```

## Acceptance criteria

- [ ] All 6 loop nodes compile and are discoverable
- [ ] ForLoop iterates exactly N times, outputs Index
- [ ] ForLoopStep handles positive and negative steps, guards against step=0
- [ ] ForEach iterates over SerializableList items
- [ ] While/DoWhile/RepeatUntil have MaxIterations safety guards
- [ ] All loops support cancellation via `CancellationToken`
- [ ] Each loop triggers "LoopPath" per iteration and "Exit" after completion
