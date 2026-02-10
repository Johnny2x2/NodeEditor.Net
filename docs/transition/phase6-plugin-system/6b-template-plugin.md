# 6B — Template Plugin Rewrite

> **Parallelism**: Can run in parallel with **6A**, **6C**, **6D**.

## Prerequisites
- **Phase 1** complete (`NodeBase`, `INodeBuilder`)

## Can run in parallel with
- **6A** (Plugin Loader), **6C** (TestA), **6D** (TestB)

## Deliverable

### Rewrite `TemplatePlugin.cs`

**File**: `NodeEditor.Plugins.Template/TemplatePlugin.cs`

**Old**:
```csharp
public class TemplatePlugin : INodePlugin { /* ... */ }

public class TemplatePluginContext : INodeContext
{
    [Node("Echo", "Template", "Template", "Echoes the input.")]
    public string Echo(string Value) => Value;
}
```

**New**:
```csharp
public class TemplatePlugin : INodePlugin
{
    // ... properties unchanged ...
    public void Register(INodeRegistryService registry) =>
        registry.RegisterFromAssembly(typeof(TemplatePlugin).Assembly);
}

public sealed class EchoNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Echo").Category("Template")
            .Description("Echoes the input.")
            .Input<string>("Value", "").Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("Value"));
        return Task.CompletedTask;
    }
}
```

**Delete**: `TemplatePluginContext` class (or the file containing it).

## Acceptance criteria

- [ ] `TemplatePlugin.Register()` call is unchanged
- [ ] `TemplatePluginContext` removed, replaced by `EchoNode : NodeBase`
- [ ] `EchoNode` is discoverable by assembly scanning
- [ ] Plugin loads and registers "Echo" node successfully
