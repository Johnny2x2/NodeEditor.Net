# Phase 4 — Standard Nodes Migration

> **Goal**: Migrate all ~40+ standard nodes from the `StandardNodeContext` partial-class + `[Node]` attribute system to either `NodeBase` subclasses (for stateful/control-flow nodes) or inline `NodeBuilder` registrations (for pure-function data nodes).

## Strategy

| Node category | Count | Approach | Why |
|--------------|-------|----------|-----|
| Control flow (loops, branches) | 8 | `NodeBase` subclasses | Need real control flow in `ExecuteAsync()` |
| Helpers (Start, Marker, Delay, Consume) | 4 | `NodeBase` subclasses | Callable with simple logic |
| Debug (Print, Warning, Error) | 4 | `NodeBase` subclasses | Need feedback emission |
| Numbers (Abs, Min, Max, etc.) | 10 | Inline `NodeBuilder` lambdas | Pure functions, no state |
| Strings (Concat, Length, etc.) | 13 | Inline `NodeBuilder` lambdas | Pure functions, no state |
| Lists (Create, Add, Get, etc.) | 12 | Inline `NodeBuilder` lambdas | Pure functions, no state |

---

## 4.1 Control Flow Nodes — `NodeBase` subclasses

### File: `NodeEditor.Net/Services/Execution/StandardNodes/StartNode.cs`

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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/BranchNode.cs`

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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/ForLoopNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop")
            .Category("Conditions")
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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/ForLoopStepNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForLoopStepNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("For Loop Step")
            .Category("Conditions")
            .Description("Iterates from start to end with a configurable step.")
            .Callable()
            .Input<int>("StartValue", 0)
            .Input<int>("EndValue", 10)
            .Input<int>("Step", 1)
            .Output<int>("Index")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var start = context.GetInput<int>("StartValue");
        var end = context.GetInput<int>("EndValue");
        var step = context.GetInput<int>("Step");

        if (step == 0) step = 1; // Guard against infinite loop

        for (int i = start; (step > 0 ? i < end : i > end) && !ct.IsCancellationRequested; i += step)
        {
            context.SetOutput("Index", i);
            await context.TriggerAsync("LoopPath");
        }

        await context.TriggerAsync("Exit");
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/ForEachLoopNode.cs`

```csharp
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ForEachLoopNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("ForEach Loop")
            .Category("Conditions")
            .Description("Iterates over each item in a list.")
            .Callable()
            .Input<SerializableList>("List")
            .Output<object>("Obj")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/WhileLoopNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class WhileLoopNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("While Loop")
            .Category("Conditions")
            .Description("Loops while a condition is true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/DoWhileLoopNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DoWhileLoopNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Do While Loop")
            .Category("Conditions")
            .Description("Executes body at least once, then loops while condition is true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        do
        {
            ct.ThrowIfCancellationRequested();
            await context.TriggerAsync("LoopPath");
            iteration++;
        }
        while (context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested);

        await context.TriggerAsync("Exit");
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/RepeatUntilNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class RepeatUntilNode : NodeBase
{
    private const int MaxIterations = 10_000;

    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Repeat Until")
            .Category("Conditions")
            .Description("Repeats body until condition becomes true.")
            .Callable()
            .Input<bool>("Condition")
            .ExecutionOutput("LoopPath")
            .ExecutionOutput("Exit");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        int iteration = 0;
        do
        {
            ct.ThrowIfCancellationRequested();
            await context.TriggerAsync("LoopPath");
            iteration++;
        }
        while (!context.GetInput<bool>("Condition") && iteration < MaxIterations && !ct.IsCancellationRequested);

        await context.TriggerAsync("Exit");
    }
}
```

---

## 4.2 Helper Nodes — `NodeBase` subclasses

### File: `NodeEditor.Net/Services/Execution/StandardNodes/MarkerNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class MarkerNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Marker")
            .Category("Helpers")
            .Description("Passthrough marker for organizing flows.")
            .Callable();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/ConsumeNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class ConsumeNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Consume")
            .Category("Helpers")
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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/DelayNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DelayNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Delay")
            .Category("Helpers")
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

---

## 4.3 Debug Nodes — `NodeBase` subclasses

### File: `NodeEditor.Net/Services/Execution/StandardNodes/DebugPrintNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugPrintNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Debug Print")
            .Category("Debug")
            .Description("Prints a labeled value to the debug output.")
            .Callable()
            .Input<string>("Label", "")
            .Input<object>("Value");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var label = context.GetInput<string>("Label");
        var value = context.GetInput<object>("Value");
        var message = string.IsNullOrEmpty(label) ? $"{value}" : $"{label}: {value}";
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        await context.TriggerAsync("Exit");
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/PrintValueNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Data-only version of Debug Print — no execution sockets.
/// Evaluates lazily when downstream reads the output.
/// </summary>
public sealed class PrintValueNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Print Value")
            .Category("Debug")
            .Description("Prints a value and passes it through.")
            .Input<string>("Label", "")
            .Input<object>("Value")
            .Output<object>("PassThrough");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var label = context.GetInput<string>("Label");
        var value = context.GetInput<object>("Value");
        var message = string.IsNullOrEmpty(label) ? $"{value}" : $"{label}: {value}";
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        context.SetOutput("PassThrough", value);
        return Task.CompletedTask;
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/DebugWarningNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugWarningNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Debug Warning")
            .Category("Debug")
            .Description("Emits a warning message.")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        context.EmitFeedback(message, ExecutionFeedbackType.Continue);
        await context.TriggerAsync("Exit");
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/DebugErrorNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugErrorNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Debug Error")
            .Category("Debug")
            .Description("Emits an error message.")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        context.EmitFeedback(message, ExecutionFeedbackType.Break);
        await context.TriggerAsync("Exit");
    }
}
```

---

## 4.4 Data Nodes — Inline `NodeBuilder` lambdas

These are registered as a batch in a static registration class. No individual class files needed.

### File: `NodeEditor.Net/Services/Execution/StandardNodes/StandardNumberNodes.cs`

```csharp
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Registers all standard number/math data nodes as inline lambda definitions.
/// </summary>
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

### File: `NodeEditor.Net/Services/Execution/StandardNodes/StandardStringNodes.cs`

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardStringNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("String Concat")
            .Category("Strings").Description("Concatenates two strings.")
            .Input<string>("A", "").Input<string>("B", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<string>("A") + ctx.GetInput<string>("B")))
            .Build();

        yield return NodeBuilder.Create("String Length")
            .Category("Strings").Description("Returns the length of a string.")
            .Input<string>("Value", "").Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Length))
            .Build();

        yield return NodeBuilder.Create("Substring")
            .Category("Strings").Description("Extracts a substring.")
            .Input<string>("Value", "").Input<int>("Start", 0).Input<int>("Length", -1).Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<string>("Value") ?? "";
                var start = Math.Max(0, Math.Min(ctx.GetInput<int>("Start"), value.Length));
                var length = ctx.GetInput<int>("Length");
                ctx.SetOutput("Result", length < 0 ? value[start..] : value.Substring(start, Math.Min(length, value.Length - start)));
            })
            .Build();

        yield return NodeBuilder.Create("Replace")
            .Category("Strings").Description("Replaces occurrences of a substring.")
            .Input<string>("Value", "").Input<string>("Old", "").Input<string>("New", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Replace(ctx.GetInput<string>("Old") ?? "", ctx.GetInput<string>("New") ?? "")))
            .Build();

        yield return NodeBuilder.Create("To Upper")
            .Category("Strings").Description("Converts to uppercase.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").ToUpperInvariant()))
            .Build();

        yield return NodeBuilder.Create("To Lower")
            .Category("Strings").Description("Converts to lowercase.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").ToLowerInvariant()))
            .Build();

        yield return NodeBuilder.Create("Trim")
            .Category("Strings").Description("Trims whitespace.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Trim()))
            .Build();

        yield return NodeBuilder.Create("Contains")
            .Category("Strings").Description("Checks if string contains a substring.")
            .Input<string>("Value", "").Input<string>("Search", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Contains(ctx.GetInput<string>("Search") ?? "")))
            .Build();

        yield return NodeBuilder.Create("Starts With")
            .Category("Strings").Description("Checks if string starts with a prefix.")
            .Input<string>("Value", "").Input<string>("Prefix", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").StartsWith(ctx.GetInput<string>("Prefix") ?? "")))
            .Build();

        yield return NodeBuilder.Create("Ends With")
            .Category("Strings").Description("Checks if string ends with a suffix.")
            .Input<string>("Value", "").Input<string>("Suffix", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").EndsWith(ctx.GetInput<string>("Suffix") ?? "")))
            .Build();

        yield return NodeBuilder.Create("Split")
            .Category("Strings").Description("Splits a string by a delimiter into a list.")
            .Input<string>("Value", "").Input<string>("Delimiter", ",").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var parts = (ctx.GetInput<string>("Value") ?? "").Split(ctx.GetInput<string>("Delimiter") ?? ",");
                ctx.SetOutput("Result", SerializableList.From(parts.Cast<object>()));
            })
            .Build();

        yield return NodeBuilder.Create("Join")
            .Category("Strings").Description("Joins a list into a string with a separator.")
            .Input<SerializableList>("List").Input<string>("Separator", ", ").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var items = list?.Items?.Select(i => i?.ToString() ?? "") ?? Enumerable.Empty<string>();
                ctx.SetOutput("Result", string.Join(ctx.GetInput<string>("Separator") ?? ", ", items));
            })
            .Build();
    }
}
```

### File: `NodeEditor.Net/Services/Execution/StandardNodes/StandardListNodes.cs`

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardListNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("List Create")
            .Category("Lists").Description("Creates a new empty list.")
            .Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", new SerializableList()))
            .Build();

        yield return NodeBuilder.Create("List Add")
            .Category("Lists").Description("Adds an item to a list and returns the new list.")
            .Input<SerializableList>("List").Input<object>("Item").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List") ?? new SerializableList();
                var clone = list.Clone();
                clone.Add(ctx.GetInput<object>("Item"));
                ctx.SetOutput("Result", clone);
            })
            .Build();

        yield return NodeBuilder.Create("List Insert")
            .Category("Lists").Description("Inserts an item at an index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Input<object>("Item").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List") ?? new SerializableList();
                var clone = list.Clone();
                clone.Insert(ctx.GetInput<int>("Index"), ctx.GetInput<object>("Item"));
                ctx.SetOutput("Result", clone);
            })
            .Build();

        yield return NodeBuilder.Create("List Remove At")
            .Category("Lists").Description("Removes item at index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List") ?? new SerializableList();
                var clone = list.Clone();
                clone.RemoveAt(ctx.GetInput<int>("Index"));
                ctx.SetOutput("Result", clone);
            })
            .Build();

        yield return NodeBuilder.Create("List Remove Value")
            .Category("Lists").Description("Removes the first occurrence of a value.")
            .Input<SerializableList>("List").Input<object>("Value").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List") ?? new SerializableList();
                var clone = list.Clone();
                clone.Remove(ctx.GetInput<object>("Value"));
                ctx.SetOutput("Result", clone);
            })
            .Build();

        yield return NodeBuilder.Create("List Clear")
            .Category("Lists").Description("Returns an empty list.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", new SerializableList()))
            .Build();

        yield return NodeBuilder.Create("List Contains")
            .Category("Lists").Description("Checks if list contains a value.")
            .Input<SerializableList>("List").Input<object>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list?.Contains(ctx.GetInput<object>("Value")) ?? false);
            })
            .Build();

        yield return NodeBuilder.Create("List Index Of")
            .Category("Lists").Description("Returns the index of a value, or -1.")
            .Input<SerializableList>("List").Input<object>("Value").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list?.IndexOf(ctx.GetInput<object>("Value")) ?? -1);
            })
            .Build();

        yield return NodeBuilder.Create("List Count")
            .Category("Lists").Description("Returns the number of items.")
            .Input<SerializableList>("List").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list?.Count ?? 0);
            })
            .Build();

        yield return NodeBuilder.Create("List Get")
            .Category("Lists").Description("Gets an item by index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Output<object>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var index = ctx.GetInput<int>("Index");
                ctx.SetOutput("Result", list?.Get(index));
            })
            .Build();

        yield return NodeBuilder.Create("List Set")
            .Category("Lists").Description("Sets an item at an index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Input<object>("Value").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List") ?? new SerializableList();
                var clone = list.Clone();
                clone.Set(ctx.GetInput<int>("Index"), ctx.GetInput<object>("Value"));
                ctx.SetOutput("Result", clone);
            })
            .Build();

        yield return NodeBuilder.Create("List Slice")
            .Category("Lists").Description("Returns a sub-list.")
            .Input<SerializableList>("List").Input<int>("Start", 0).Input<int>("Count", -1).Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var start = ctx.GetInput<int>("Start");
                var count = ctx.GetInput<int>("Count");
                ctx.SetOutput("Result", list?.Slice(start, count) ?? new SerializableList());
            })
            .Build();
    }
}
```

---

## 4.5 Standard Node Registration

### File: `NodeEditor.Net/Services/Execution/StandardNodes/StandardNodeRegistration.cs` (new)

```csharp
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Collects all inline (lambda) standard node definitions.
/// Called during NodeRegistryService initialization.
/// </summary>
public static class StandardNodeRegistration
{
    public static IEnumerable<NodeDefinition> GetInlineDefinitions()
    {
        foreach (var def in StandardNumberNodes.GetDefinitions()) yield return def;
        foreach (var def in StandardStringNodes.GetDefinitions()) yield return def;
        foreach (var def in StandardListNodes.GetDefinitions()) yield return def;
    }
}
```

The `NodeRegistryService.EnsureInitialized()` will:
1. Scan assemblies for `NodeBase` subclasses → discovers all class-based nodes (Start, Branch, loops, debug, helpers)
2. Call `StandardNodeRegistration.GetInlineDefinitions()` → registers all data nodes
3. Call `RegisterDefinitions()` for both sets

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **Create** | `StandardNodes/StartNode.cs` | ExecInit |
| **Create** | `StandardNodes/BranchNode.cs` | Condition branching |
| **Create** | `StandardNodes/ForLoopNode.cs` | Simple for loop |
| **Create** | `StandardNodes/ForLoopStepNode.cs` | Stepped for loop |
| **Create** | `StandardNodes/ForEachLoopNode.cs` | List iteration |
| **Create** | `StandardNodes/WhileLoopNode.cs` | While loop |
| **Create** | `StandardNodes/DoWhileLoopNode.cs` | Do-while loop |
| **Create** | `StandardNodes/RepeatUntilNode.cs` | Repeat-until loop |
| **Create** | `StandardNodes/MarkerNode.cs` | Passthrough |
| **Create** | `StandardNodes/ConsumeNode.cs` | Force evaluation |
| **Create** | `StandardNodes/DelayNode.cs` | Timed delay |
| **Create** | `StandardNodes/DebugPrintNode.cs` | Debug output |
| **Create** | `StandardNodes/PrintValueNode.cs` | Data-only debug |
| **Create** | `StandardNodes/DebugWarningNode.cs` | Warning feedback |
| **Create** | `StandardNodes/DebugErrorNode.cs` | Error feedback |
| **Create** | `StandardNodes/StandardNumberNodes.cs` | 10 math inline nodes |
| **Create** | `StandardNodes/StandardStringNodes.cs` | 12 string inline nodes |
| **Create** | `StandardNodes/StandardListNodes.cs` | 12 list inline nodes |
| **Create** | `StandardNodes/StandardNodeRegistration.cs` | Aggregator |
| **Delete** | `StandardNodes/StandardNodeContext.cs` | Old shared context |
| **Delete** | `StandardNodes/StandardNodeContext.Conditions.cs` | Old conditions partial |
| **Delete** | `StandardNodes/StandardNodeContext.Helpers.cs` | Old helpers partial |
| **Delete** | `StandardNodes/StandardNodeContext.DebugPrint.cs` | Old debug partial |
| **Delete** | `StandardNodes/StandardNodeContext.Numbers.cs` | Old numbers partial |
| **Delete** | `StandardNodes/StandardNodeContext.Strings.cs` | Old strings partial |
| **Delete** | `StandardNodes/StandardNodeContext.Lists.cs` | Old lists partial |
| **Delete** | `StandardNodes/StandardNodeContext.Parallel.cs` | Old parallel partial (empty) |

## Dependencies

- Depends on Phase 1 (`NodeBase`, `INodeBuilder`, `NodeBuilder`, `INodeExecutionContext`)
- Depends on Phase 3 (execution engine must support `TriggerAsync()` dispatch)
- Phase 5 (cleanup) depends on all standard nodes being migrated
