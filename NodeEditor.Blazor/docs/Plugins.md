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
└── Registers discovered nodes with NodeRegistryService

INodePlugin
├── Defines plugin metadata (ID, Name, Version)
└── Handles registration logic

INodeProvider (optional)
└── Provides custom NodeDefinition factories

INodeContext
└── Contains methods decorated with [Node] attribute
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

    public void Register(NodeRegistryService registry)
    {
        // Register all types from this assembly
        registry.RegisterFromAssembly(typeof(MyPlugin).Assembly);
    }
}
```

### Step 2: Create a Node Context

Create a class implementing `INodeContext` with node methods:

```csharp
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace MyCompany.NodeEditor.Plugins;

public sealed class MyPluginContext : INodeContext
{
    [Node("Add Numbers", category: "Math", description: "Add two numbers together")]
    public void AddNumbers(double A, double B, out double Result)
    {
        Result = A + B;
    }

    [Node("Format Text", category: "String", description: "Format a string with value")]
    public void FormatText(string Template, double Value, out string Result)
    {
        Result = string.Format(Template, Value);
    }

    [Node("Log Message", category: "Debug", description: "Log a message",
          isCallable: true, isExecutionInitiator: false)]
    public void LogMessage(ExecutionPath Entry, string Message, out ExecutionPath Exit)
    {
        Console.WriteLine($"[LOG] {Message}");
        Exit = new ExecutionPath();
        Exit.Signal();
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

## Node Attribute

The `[Node]` attribute defines node metadata:

```csharp
[Node(
    name: "Node Name",              // Display name (required)
    category: "Category",           // Category for grouping (optional)
    description: "Description",     // Tooltip description (optional)
    isCallable: false,              // Can be executed (default: false)
    isExecutionInitiator: false     // Entry point for execution (default: false)
)]
public void MyNode(/* parameters */)
{
    // Implementation
}
```

### Parameter Types

**Input Parameters** (standard parameters):
- Appear as input sockets on the left
- Support primitive types: `int`, `double`, `float`, `string`, `bool`
- Support `ExecutionPath` for execution flow

**Output Parameters** (`out` parameters):
- Appear as output sockets on the right
- Use `out` keyword
- Support same types as input parameters

### Examples

**Data Processing Node:**
```csharp
[Node("Multiply", category: "Math")]
public void Multiply(double A, double B, out double Result)
{
    Result = A * B;
}
```

**Execution Flow Node:**
```csharp
[Node("Print", category: "Debug", isCallable: true)]
public void Print(ExecutionPath Entry, string Text, out ExecutionPath Exit)
{
    Console.WriteLine(Text);
    Exit = new ExecutionPath();
    Exit.Signal();
}
```

**Multiple Outputs:**
```csharp
[Node("Compare", category: "Math")]
public void Compare(double A, double B, out bool Greater, out bool Equal, out bool Less)
{
    Greater = A > B;
    Equal = Math.Abs(A - B) < double.Epsilon;
    Less = A < B;
}
```

**Conditional Branch:**
```csharp
[Node("Branch", category: "Flow", isCallable: true)]
public void Branch(ExecutionPath Entry, bool Condition,
                   out ExecutionPath True, out ExecutionPath False)
{
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

    public void Register(NodeRegistryService registry)
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

For plugins that need persistent state:

```csharp
public sealed class StatefulContext : INodeContext
{
    private readonly Dictionary<string, int> _counters = new();

    [Node("Counter", category: "Utility")]
    public void Counter(string CounterName, out int Value)
    {
        if (!_counters.ContainsKey(CounterName))
            _counters[CounterName] = 0;

        _counters[CounterName]++;
        Value = _counters[CounterName];
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
@inject NodeRegistryService Registry

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
    public string Id => "com.nodeeditormax.template";
    public string Id => "com.nodeeditormax.sample";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(SamplePlugin).Assembly);
    }
}

public sealed class SamplePluginContext : INodeContext
{
    [Node("Multiply", category: "Math", description: "Multiply two numbers")]
    public void Multiply(double A, double B, out double Result)
    {
        Result = A * B;
    }

    [Node("Clamp", category: "Math", description: "Clamp value between min/max")]
    public void Clamp(double Value, double Min, double Max, out double Result)
    {
        Result = Math.Clamp(Value, Min, Max);
    }

    [Node("Random Int", category: "Math", description: "Random integer in range")]
    public void RandomInt(int Min, int Max, out int Result)
    {
        Result = Random.Shared.Next(Min, Max + 1);
    }

    [Node("Pulse", category: "Flow", description: "Emit an execution pulse",
          isCallable: true, isExecutionInitiator: true)]
    public void Pulse(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
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

Handle errors gracefully in node methods:
```csharp
[Node("Safe Divide", category: "Math")]
public void SafeDivide(double A, double B, out double Result, out bool Success)
{
    if (Math.Abs(B) < double.Epsilon)
    {
        Result = 0;
        Success = false;
        return;
    }

    Result = A / B;
    Success = true;
}
```

### 4. Documentation

Always provide descriptions:
```csharp
[Node(
    name: "Process Data",
    category: "Processing",
    description: "Processes input data using custom algorithm. Returns null if input is invalid."
)]
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

1. Ensure `INodeContext` is public
2. Check that methods have `[Node]` attribute
3. Verify `Register()` method is called
4. Check that assembly is properly registered

### Type Resolution Errors

1. Ensure socket types are supported (primitives or `ExecutionPath`)
2. Check for circular dependencies
3. Verify method signatures match node definition

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
    void Register(NodeRegistryService registry);
}
```

### INodeProvider

```csharp
public interface INodeProvider
{
    IEnumerable<NodeDefinition> GetNodeDefinitions();
}
```

### INodeContext

```csharp
public interface INodeContext
{
    // Marker interface for node method containers
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
