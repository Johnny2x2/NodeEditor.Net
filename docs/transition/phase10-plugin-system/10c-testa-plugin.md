# 10C — TestA Plugin Rewrite

> **Phase 10 — Plugin System**
> All sub-tasks in this phase can run fully in parallel.

## Prerequisites
- **Phases 1 2** complete (`NodeBase`, `INodeBuilder`, `INodeExecutionContext`)

## Can run in parallel with
- **10A** (Plugin Loader), **10B** (Template), **10D** (TestB)

## Deliverable

### Rewrite TestA plugin nodes

**File**: `NodeEditor.Plugins.TestA/TestAPlugin.cs`

**Old** (3 nodes via `INodeContext` + `[Node]`):
```csharp
public class TestAPluginContext : INodeContext
{
    [Node("Echo", ...)] public string Echo(string Value) => Value;
    [Node("Ping", ...)] public void Ping(out ExecutionPath Exit) { ... }
    [Node("Load Image", ...)] public byte[] LoadImage(string Path) => ...;
}
```

**New** (3 `NodeBase` subclasses):

```csharp
public sealed class EchoNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Echo").Category("Test").Description("Echoes the input.")
            .Input<string>("Value", "").Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("Value"));
        return Task.CompletedTask;
    }
}

public sealed class PingNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Ping").Category("Test").Description("Sends a ping.")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}

public sealed class LoadImageNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Load Image").Category("Test/Image").Description("Loads an image from path.")
            .Input<string>("Path", "").Output<byte[]>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var path = context.GetInput<string>("Path");
        context.SetOutput("Result", File.ReadAllBytes(path));
        return Task.CompletedTask;
    }
}
```

**Delete**: `TestAPluginContext` class. Also update any custom components/editors in `Components/` and `Editors/` if they reference removed types.

## Acceptance criteria

- [ ] `TestAPlugin.Register()` call unchanged
- [ ] 3 `NodeBase` subclasses replace `TestAPluginContext`
- [ ] All 3 nodes discoverable by assembly scanning
- [ ] `PingNode` is an `ExecutionInitiator`
- [ ] Plugin loads and registers all 3 nodes
