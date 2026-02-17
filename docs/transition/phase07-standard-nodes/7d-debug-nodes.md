# 7D — Debug Nodes

> **Phase 7 — Standard Nodes**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1–6** complete (`NodeBase`, `INodeBuilder`, `INodeExecutionContext`)

## Can run in parallel with
- **7A**, **7B**, **7C**, **7E**, **7F**, **7G**

## Deliverables

### `DebugPrintNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/DebugPrintNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugPrintNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Debug Print").Category("Debug")
            .Description("Prints a labeled value to the debug output.")
            .Callable()
            .Input<string>("Label", "").Input<object>("Value");
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

### `PrintValueNode` (data-only)

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/PrintValueNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class PrintValueNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Print Value").Category("Debug")
            .Description("Prints a value and passes it through.")
            .Input<string>("Label", "").Input<object>("Value")
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

### `DebugWarningNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/DebugWarningNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugWarningNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Debug Warning").Category("Debug")
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

### `DebugErrorNode`

**File**: `NodeEditor.Net/Services/Execution/StandardNodes/DebugErrorNode.cs`

```csharp
namespace NodeEditor.Net.Services.Execution.StandardNodes;

public sealed class DebugErrorNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Debug Error").Category("Debug")
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

## Acceptance criteria

- [ ] DebugPrint emits feedback with label+value formatting
- [ ] PrintValue is data-only (no execution sockets), passes value through
- [ ] DebugWarning uses `ExecutionFeedbackType.Continue`
- [ ] DebugError uses `ExecutionFeedbackType.Break`
- [ ] All 4 nodes compile and are discoverable
