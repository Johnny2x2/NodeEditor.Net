# Plugin System

The NodeEditor.Blazor plugin system enables dynamic extension of the node editor with custom node types loaded from external assemblies at runtime.

## Overview

The plugin system provides:
- **Dynamic Loading** - Load plugins from assemblies at runtime
- **Isolation** - Each plugin loads in its own AssemblyLoadContext
- **Validation** - Version checking and manifest validation
- **Auto-Discovery** - Automatically discover node types using reflection
- **Type Safety** - Full type safety for node methods and parameters

## Architecture

### Core Components

```
PluginLoader
├── Discovers plugin assemblies from directories
├── Validates plugin manifests and versions
├── Creates isolated AssemblyLoadContext per plugin
└── Registers discovered nodes with INodeRegistryService

INodePlugin
├── Defines plugin metadata (ID, Name, Version)
└── Handles registration logic

INodeProvider (optional)
└── Provides custom NodeDefinition factories

NodeBase
└── Abstract base class for defining nodes via Configure(INodeBuilder) + ExecuteAsync
```

## Creating a Plugin

### Step 1: Create a Plugin Class

Implement the `INodePlugin` interface:

```csharp
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace MyCompany.NodeEditor.Plugins;

public sealed class MyPlugin : INodePlugin
{
    public string Name => "My Custom Nodes";
    public string Id => "com.mycompany.myplugin";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        // Register all types from this assembly
        registry.RegisterFromAssembly(typeof(MyPlugin).Assembly);
    }
}
```

### Step 2: Create Node Classes

Create classes extending `NodeBase` for each node:

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

namespace MyCompany.NodeEditor.Plugins;

public sealed class AddNumbersNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add Numbers")
               .Category("Math")
               .Description("Add two numbers together")
               .Input<double>("A", 0.0)
               .Input<double>("B", 0.0)
               .Output<double>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");
        context.SetOutput("Result", a + b);
        return Task.CompletedTask;
    }
}

public sealed class FormatTextNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Format Text")
               .Category("String")
               .Description("Format a string with value")
               .Input<string>("Template", "")
               .Input<double>("Value", 0.0)
               .Output<string>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var template = context.GetInput<string>("Template");
        var value = context.GetInput<double>("Value");
        context.SetOutput("Result", string.Format(template, value));
        return Task.CompletedTask;
    }
}

public sealed class LogMessageNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Log Message")
               .Category("Debug")
               .Description("Log a message")
               .Input<string>("Message", "")
               .Callable(); // Adds Enter/Exit execution sockets
    }

    public override async Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        Console.WriteLine($"[LOG] {message}");
        await context.TriggerAsync("Exit");
    }
}
```

### Step 3: Create Plugin Manifest (Optional)

Create a `plugin.json` file in your plugin directory:

```json
{
  "id": "com.mycompany.myplugin",
  "name": "My Custom Nodes",
  "version": "1.0.0",
  "minApiVersion": "1.0.0",
  "description": "Custom math and string nodes",
  "author": "My Company",
  "entryAssembly": "MyCompany.NodeEditor.Plugins.dll"
}
```

### Step 4: Build and Deploy

1. Build your plugin project
2. Copy the output DLL to the plugins directory
3. Optionally include the `plugin.json` manifest

**Plugin Directory Structure:**

```
plugins/
├── MyPlugin/
│   ├── plugin.json (optional)
│   ├── MyCompany.NodeEditor.Plugins.dll
│   └── [dependencies].dll
└── AnotherPlugin/
    └── AnotherPlugin.dll
```

## Node Definition via NodeBase

Nodes are defined by subclassing `NodeBase` and overriding two methods:

- `Configure(INodeBuilder builder)` — sets metadata and declares sockets
- `ExecuteAsync(INodeExecutionContext context, CancellationToken ct)` — runs the logic

### Builder API

The fluent `INodeBuilder` provides:

| Method | Purpose |
|--------|---------|
| `Name(string)` | Display name (required) |
| `Category(string)` | Category for grouping |
| `Description(string)` | Tooltip description |
| `Input<T>(name, default, editorHint?)` | Add an input socket |
| `Output<T>(name)` | Add an output socket |
| `Callable()` | Add Enter/Exit execution sockets |
| `ExecutionInitiator()` | Add Exit socket only (entry point) |
| `ExecutionInput(name)` | Named execution input |
| `ExecutionOutput(name)` | Named execution output |
| `StreamOutput<T>()` | Add a streaming output |
| `OnExecute(Func)` | Inline execution (for simple nodes) |

### Examples

**Data Node:**
```csharp
public class MultiplyNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Multiply")
               .Category("Math")
               .Input<double>("A", 0.0)
               .Input<double>("B", 0.0)
               .Output<double>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");
        context.SetOutput("Result", a * b);
        return Task.CompletedTask;
    }
}
```

**Callable Node:**
```csharp
public class PrintNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Print")
               .Category("Debug")
               .Input<string>("Text", "")
               .Callable();
    }

    public override async Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var text = context.GetInput<string>("Text");
        Console.WriteLine(text);
        await context.TriggerAsync("Exit");
    }
}
```

**Multiple Outputs:**
```csharp
public class CompareNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Compare")
               .Category("Math")
               .Input<double>("A", 0.0)
               .Input<double>("B", 0.0)
               .Output<bool>("Greater")
               .Output<bool>("Equal")
               .Output<bool>("Less");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");
        context.SetOutput("Greater", a > b);
        context.SetOutput("Equal", Math.Abs(a - b) < double.Epsilon);
        context.SetOutput("Less", a < b);
        return Task.CompletedTask;
    }
}
```

**Conditional Branch:**
```csharp
public class BranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Branch")
               .Category("Flow")
               .Input<bool>("Condition", false)
               .Callable()
               .ExecutionOutput("True")
               .ExecutionOutput("False");
    }

    public override async Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var condition = context.GetInput<bool>("Condition");
        if (condition)
            await context.TriggerAsync("True");
        else
            await context.TriggerAsync("False");
    }
}
    True = new ExecutionPath();
    False = new ExecutionPath();

    if (Condition)
        True.Signal();
    else
        False.Signal();
}
```

## Advanced Plugin Features

### Custom Node Provider

Implement `INodeProvider` for complete control over node creation:

```csharp
public sealed class AdvancedPlugin : INodePlugin, INodeProvider
{
    public string Name => "Advanced Plugin";
    public string Id => "com.example.advanced";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterDefinitions(GetNodeDefinitions());
    }

    public IEnumerable<NodeDefinition> GetNodeDefinitions()
    {
        yield return new NodeDefinition(
            Id: "Advanced.CustomNode",
            Name: "Custom Node",
            Category: "Advanced",
            Description: "A fully customized node",
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString(),
                Name: "Custom Node",
                Callable: false,
                ExecInit: false,
                Inputs: new List<SocketData>
                {
                    new("Input1", "string", false, false, new SocketValue("")),
                    new("Input2", "int", false, false, new SocketValue(0))
                },
                Outputs: new List<SocketData>
                {
                    new("Output", "object", false, true, new SocketValue(null))
                }
            )
        );
    }
}
```

### Plugin Dependencies

If your plugin depends on other assemblies:

1. Include them in the plugin directory
2. They will be loaded automatically from the plugin's AssemblyLoadContext
3. Ensure all dependencies are compatible with the host application's .NET version

### State Management in Plugins

For nodes that need persistent state, use fields in the `NodeBase` subclass:

```csharp
public sealed class CounterNode : NodeBase
{
    private int _count;

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Counter")
               .Category("Utility")
               .Input<string>("CounterName", "default")
               .Output<int>("Value")
               .Callable();
    }

    public override async Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        _count++;
        context.SetOutput("Value", _count);
        await context.TriggerAsync("Exit");
    }
}
```

**Note:** Context instances are shared across node executions within the same execution context.

## Loading Plugins

### At Application Startup

```csharp
// In Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure plugin options
builder.Services.Configure<PluginOptions>(options =>
{
    options.EnablePluginLoading = true;
    options.PluginDirectory = "plugins";
    options.ApiVersion = new Version(1, 0, 0);
});

builder.Services.AddNodeEditor();

var app = builder.Build();

// Load plugins on startup
using (var scope = app.Services.CreateScope())
{
    var pluginLoader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
    var plugins = await pluginLoader.LoadAndRegisterAsync();

    Console.WriteLine($"Loaded {plugins.Count} plugins:");
    foreach (var plugin in plugins)
    {
        Console.WriteLine($"  - {plugin.Name} (v{plugin.Version})");
    }
}

app.Run();
```

### At Runtime

```csharp
@inject PluginLoader PluginLoader
@inject INodeRegistryService Registry

private async Task LoadPluginsAsync()
{
    try
    {
        var plugins = await PluginLoader.LoadAndRegisterAsync("./custom-plugins");

        foreach (var plugin in plugins)
        {
            Console.WriteLine($"Loaded: {plugin.Name}");
        }

        // Registry will fire RegistryChanged event
        // Components can subscribe to update their UI
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error loading plugins: {ex.Message}");
    }
}
```

## Plugin Configuration

### PluginOptions

Configure plugin loading behavior:

```csharp
public class PluginOptions
{
    public const string SectionName = "Plugins";

    // Enable/disable plugin loading
    public bool EnablePluginLoading { get; set; } = true;

    // Directory to scan for plugins
    public string PluginDirectory { get; set; } = "plugins";

    // Manifest filename to look for
    public string ManifestFileName { get; set; } = "plugin.json";

    // Host API version for compatibility checking
    public Version ApiVersion { get; set; } = new(1, 0, 0);
}
```

### Configuration in appsettings.json

```json
{
  "Plugins": {
    "EnablePluginLoading": true,
    "PluginDirectory": "plugins",
    "ManifestFileName": "plugin.json",
    "ApiVersion": "1.0.0"
  }
}
```

## Plugin Validation

The plugin loader validates plugins before loading:

### Version Compatibility

```csharp
// Plugin requires API v2.0, host provides v1.0
if (plugin.MinApiVersion > hostOptions.ApiVersion)
{
    // Plugin rejected: "Requires API 2.0.0 (host 1.0.0)"
}
```

### Manifest Validation

If a `plugin.json` exists, the loader validates:
- Plugin ID matches manifest ID
- Plugin name matches manifest name
- Manifest API version is compatible

### Error Handling

Plugin loading errors are logged but don't crash the application:

```csharp
try
{
    // Load plugin
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Failed to load plugin from '{Path}'", assemblyPath);
    // Continue loading other plugins
}
```

## Plugin Template

See the included template plugin for a complete example:

**Location:** `NodeEditor.Plugins.Template/TemplatePlugin.cs`

```csharp
public sealed class TemplatePlugin : INodePlugin
{
    public string Id => "com.nodeeditor.template";
    public string Name => "Template Plugin";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(TemplatePlugin).Assembly);
    }
}

public sealed class MultiplyNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Multiply")
               .Category("Math")
               .Description("Multiply two numbers")
               .Input<double>("A", 0.0)
               .Input<double>("B", 0.0)
               .Output<double>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");
        context.SetOutput("Result", a * b);
        return Task.CompletedTask;
    }
}

public sealed class ClampNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Clamp")
               .Category("Math")
               .Description("Clamp value between min/max")
               .Input<double>("Value", 0.0)
               .Input<double>("Min", 0.0)
               .Input<double>("Max", 1.0)
               .Output<double>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var value = context.GetInput<double>("Value");
        var min = context.GetInput<double>("Min");
        var max = context.GetInput<double>("Max");
        context.SetOutput("Result", Math.Clamp(value, min, max));
        return Task.CompletedTask;
    }
}

public sealed class RandomIntNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Random Int")
               .Category("Math")
               .Description("Random integer in range")
               .Input<int>("Min", 0)
               .Input<int>("Max", 100)
               .Output<int>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var min = context.GetInput<int>("Min");
        var max = context.GetInput<int>("Max");
        context.SetOutput("Result", Random.Shared.Next(min, max + 1));
        return Task.CompletedTask;
    }
}

public sealed class PulseNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Pulse")
               .Category("Flow")
               .Description("Emit an execution pulse")
               .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
```

## Best Practices

### 1. Plugin IDs

Use reverse domain notation:
```csharp
public string Id => "com.company.product.plugin";
```

### 2. Versioning

Follow semantic versioning:
- **Major**: Breaking changes
- **Minor**: New features, backward compatible
- **Patch**: Bug fixes

### 3. Error Handling

Handle errors gracefully in node execution:
```csharp
public class SafeDivideNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Safe Divide")
               .Category("Math")
               .Input<double>("A", 0.0)
               .Input<double>("B", 1.0)
               .Output<double>("Result")
               .Output<bool>("Success");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");

        if (Math.Abs(b) < double.Epsilon)
        {
            context.SetOutput("Result", 0.0);
            context.SetOutput("Success", false);
        }
        else
        {
            context.SetOutput("Result", a / b);
            context.SetOutput("Success", true);
        }

        return Task.CompletedTask;
    }
}
```

### 4. Documentation

Always provide descriptions via the builder:
```csharp
builder.Name("Process Data")
       .Category("Processing")
       .Description("Processes input data using custom algorithm. Returns null if input is invalid.");
```

### 5. Testing

Create unit tests for your plugin nodes:
```csharp
[Fact]
public void Multiply_TwoNumbers_ReturnsProduct()
{
    var context = new SamplePluginContext();
    context.Multiply(5, 7, out var result);
    Assert.Equal(35, result);
}
```

## Platform Considerations

### WASM Limitations

Plugin loading is **not supported** on Blazor WebAssembly or iOS due to platform restrictions:

```csharp
if (!PlatformGuard.IsPluginLoadingSupported())
{
    // Plugin loading is disabled on this platform
}
```

Supported platforms:
- Blazor Server
- Windows Desktop
- macOS Desktop
- Linux Desktop
- Android

### Security

Plugins run with the same permissions as the host application. Only load plugins from trusted sources.

## Troubleshooting

### Plugin Not Loading

1. Check the plugin directory path
2. Verify the DLL is not corrupted
3. Check logs for version incompatibility
4. Ensure plugin implements `INodePlugin`
5. Verify manifest JSON is valid

### Nodes Not Appearing

1. Ensure `NodeBase` subclass is `public`
2. Check that `Configure(INodeBuilder)` is overridden
3. Verify `Register()` method calls `RegisterFromAssembly`
4. Check that assembly is properly registered

### Type Resolution Errors

1. Ensure socket types are supported (primitives, strings, or custom types registered with `SocketTypeResolver`)
2. Check for circular dependencies
3. Verify `Configure` builder calls match `ExecuteAsync` input/output names

### Version Conflicts

Update plugin's `MinApiVersion` to match host:
```csharp
public Version MinApiVersion => new(1, 0, 0); // Match host version
```

## API Reference

### INodePlugin

```csharp
public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }
    void Register(INodeRegistryService registry);
}
```

### INodeProvider

```csharp
public interface INodeProvider
{
    IEnumerable<NodeDefinition> GetNodeDefinitions();
}
```

### NodeBase

```csharp
public abstract class NodeBase
{
    public abstract void Configure(INodeBuilder builder);
    public abstract Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct);
}
```

### PluginLoader

```csharp
public sealed class PluginLoader
{
    public Task<IReadOnlyList<INodePlugin>> LoadAndRegisterAsync(
        string? pluginDirectory = null,
        CancellationToken token = default);

    public Task<IReadOnlyList<INodePlugin>> LoadPluginsAsync(
        string? pluginDirectory = null,
        CancellationToken token = default);
}
```
