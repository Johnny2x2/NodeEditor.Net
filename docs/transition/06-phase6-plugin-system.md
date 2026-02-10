# Phase 6 — Plugin System Updates

> **Goal**: Update the plugin contracts, loader, and existing plugin projects to use `NodeBase` subclasses instead of `INodeContext` + `[Node]` attributes.

## 6.1 Update `INodePlugin`

**File**: `NodeEditor.Net/Services/Plugins/INodePlugin.cs`

**No changes to the interface itself** — it's already generic enough:

```csharp
public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }

    void Register(INodeRegistryService registry);  // ← plugins call registry.RegisterFromAssembly()
    // ...
}
```

Plugins call `registry.RegisterFromAssembly(typeof(MyPlugin).Assembly)` in `Register()`, which now scans for `NodeBase` subclasses instead of `INodeContext` types. **No API break for plugins** — the call site is the same, only the discovery behavior changes.

---

## 6.2 Update `INodeProvider`

**File**: `NodeEditor.Net/Services/Plugins/INodeProvider.cs`

**No changes needed** — it already returns `IEnumerable<NodeDefinition>`. Plugins that implement `INodeProvider` can now return definitions built with `NodeBuilder`:

```csharp
public class MyPlugin : INodePlugin, INodeProvider
{
    public IEnumerable<NodeDefinition> GetNodeDefinitions()
    {
        yield return NodeBuilder.Create("Custom Data Node")
            .Category("My Plugin")
            .Input<string>("Input")
            .Output<string>("Output")
            .OnExecute(async (ctx, ct) =>
            {
                ctx.SetOutput("Output", ctx.GetInput<string>("Input").ToUpper());
            })
            .Build();
    }
}
```

---

## 6.3 Rewrite `PluginLoader`

**File**: `NodeEditor.Net/Services/Plugins/PluginLoader.cs`

### Changes

**Remove**:
- `RegisterNodeContextsFromAssembly()` — scanned for `INodeContext`/`INodeMethodContext` types by both type assignability AND interface name (cross-assembly compat hack). No longer needed.
- All references to `INodeContextRegistry` — context registration is removed
- Cross-assembly reflection hacks for `NodeAttribute` name checking

**Update**:
- `LoadCandidate()`: after loading the plugin assembly, find types that:
  - Implement `INodePlugin` (unchanged)
  - Are `NodeBase` subclasses — the registry will discover these automatically via `RegisterFromAssembly()`
- In `LoadAndRegisterAsync()` / per-plugin post-load:
  - `plugin.Register(registry)` — unchanged (plugin calls `registry.RegisterFromAssembly()`)
  - If plugin is `INodeProvider`, call `provider.GetNodeDefinitions()` and register — unchanged
  - **Remove**: `RegisterNodeContextsFromAssembly(_contextRegistry, entry.Assembly)` call
  - **Remove**: `_contextRegistry.Unregister()` calls in `UnloadPluginAsync()`

**Simplified unload**:
- Remove context unregistration (was: `_contextRegistry.Unregister(context)` for each)
- Keep: `_registry.RemoveDefinitions()`, `_registry.RemoveDefinitionsFromAssembly()`
- Keep: plugin service cleanup, editor cleanup, log channel cleanup
- Keep: `entry.LoadContext.Unload()` (assembly-level unload)

### Constructor dependency change

```diff
  public PluginLoader(
      INodeRegistryService registry,
-     INodeContextRegistry contextRegistry,
      IPluginServiceRegistry serviceRegistry,
      // ...
  )
```

Remove `INodeContextRegistry` from constructor injection.

---

## 6.4 Update `NodeEditor.Plugins.Template`

**File**: `NodeEditor.Plugins.Template/TemplatePlugin.cs`

**Current** (old system):
```csharp
public class TemplatePlugin : INodePlugin
{
    // ... properties ...
    public void Register(INodeRegistryService registry) =>
        registry.RegisterFromAssembly(typeof(TemplatePlugin).Assembly);
}

public class TemplatePluginContext : INodeContext
{
    [Node("Echo", "Template", "Template", "Echoes the input.")]
    public string Echo(string Value) => Value;
}
```

**New** (class-based):
```csharp
public class TemplatePlugin : INodePlugin
{
    // ... properties unchanged ...
    public void Register(INodeRegistryService registry) =>
        registry.RegisterFromAssembly(typeof(TemplatePlugin).Assembly);
}

// Former TemplatePluginContext is no longer needed.
// Replace with a NodeBase subclass:

public sealed class EchoNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Echo")
            .Category("Template")
            .Description("Echoes the input.")
            .Input<string>("Value", "")
            .Output<string>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<string>("Value"));
        return Task.CompletedTask;
    }
}
```

**Key point**: `registry.RegisterFromAssembly()` call is identical — only the discovered types change (from `INodeContext` with `[Node]` methods to `NodeBase` subclasses).

---

## 6.5 Update `NodeEditor.Plugins.TestA`

**File**: `NodeEditor.Plugins.TestA/TestAPlugin.cs`

**Current** (old system):
```csharp
public class TestAPlugin : INodePlugin { /* ... */ }

public class TestAPluginContext : INodeContext
{
    [Node("Echo", "Test", "Test", "Echoes the input.")]
    public string Echo(string Value) => Value;

    [Node("Ping", "Test", "Test", "Sends a ping.", isExecutionInitiator: true)]
    public void Ping(out ExecutionPath Exit) { Exit = new ExecutionPath(); Exit.Signal(); }

    [Node("Load Image", "Test", "Test/Image", "Loads an image from path.")]
    public byte[] LoadImage(string Path) => File.ReadAllBytes(Path);
}
```

**New** (class-based):
```csharp
public class TestAPlugin : INodePlugin { /* ... unchanged ... */ }

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

---

## 6.6 Update `NodeEditor.Plugins.TestB`

Same pattern — replace `INodeContext` + `[Node]` methods with `NodeBase` subclasses. Specific nodes TBD based on TestB's current content.

---

## 6.7 Plugin DI: How plugin nodes access services

In the old system, plugin node methods had no DI — they were static-like methods on shared context objects. 

In the new system, each `NodeBase` instance gets access to DI via:

1. **`context.Services`** (`IServiceProvider`) in `ExecuteAsync()` — resolves any registered service
2. **`OnCreatedAsync(IServiceProvider services)`** — one-time setup before execution

Plugin services registered via `plugin.ConfigureServices(IServiceCollection)` are available in the per-plugin `IServiceProvider` passed to `plugin.OnInitializeAsync()`. To make these available to node instances:

**Option A** (recommended): The `ExecutionRuntime` creates node instances and passes the host `IServiceProvider` (which includes host services like loggers, HTTP clients, etc.). Plugin-specific services are registered in the host container during plugin load.

**Option B**: Node instances from plugin assemblies get the plugin's `IServiceProvider` instead of the host's. This requires the `ExecutionRuntime` to know which plugin a node belongs to.

Recommendation: **Option A** for simplicity. Plugin services added via `ConfigureServices` get merged into the host container.

---

## 6.8 Cross-assembly compatibility

The old system had complex cross-assembly hacks in `NodeMethodInvoker` and `PluginLoader` to handle `NodeAttribute` types loaded in isolated `AssemblyLoadContext`s (checking by type name string when type identity failed).

The new system is simpler:
- Discovery scans for types inheriting `NodeBase` — this works cross-assembly via `type.IsSubclassOf(typeof(NodeBase))` as long as the plugin references `NodeEditor.Net.dll`
- If isolated `AssemblyLoadContext` breaks type identity for `NodeBase`, fall back to checking `type.BaseType.FullName == "NodeEditor.Net.Services.Execution.NodeBase"`
- `INodeBuilder` is an interface — same cross-assembly considerations apply

The `PluginLoader` should retain the defensive string-based type checking as a fallback, but the primary path uses proper type system checks.

---

## Files impacted by this phase

| Action | File | Notes |
|--------|------|-------|
| **No change** | `NodeEditor.Net/Services/Plugins/INodePlugin.cs` | Interface unchanged |
| **No change** | `NodeEditor.Net/Services/Plugins/INodeProvider.cs` | Interface unchanged |
| **Modify** | `NodeEditor.Net/Services/Plugins/PluginLoader.cs` | Remove `INodeContextRegistry` usage, remove `RegisterNodeContextsFromAssembly` |
| **Rewrite** | `NodeEditor.Plugins.Template/TemplatePlugin.cs` | Replace `INodeContext` + `[Node]` with `NodeBase` subclass |
| **Rewrite** | `NodeEditor.Plugins.TestA/TestAPlugin.cs` | Replace context class with `NodeBase` subclasses |
| **Rewrite** | `NodeEditor.Plugins.TestB/` | Same pattern |
| **Delete** | Any `*PluginContext.cs` files in plugin projects | No longer needed |

## Dependencies

- Depends on Phase 1 (`NodeBase`, `INodeBuilder`, `NodeBuilder`)
- Depends on Phase 2 (discovery scans for `NodeBase`)
- Depends on Phase 5 (`INodeContext` removed — plugins must stop implementing it)
- Can run in parallel with Phase 4 (standard nodes) and Phase 7 (Blazor)
