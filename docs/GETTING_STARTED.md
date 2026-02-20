# Getting Started with NodeEditor.Net

This guide walks you through setting up NodeEditor.Net, creating your first node graph, and running it.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A code editor (Visual Studio, VS Code, Rider, etc.)

## Step 1: Create a New Blazor Project

```bash
dotnet new blazor -n MyNodeEditor --interactivity Server
cd MyNodeEditor
```

## Step 2: Add NodeEditor.Net References

Add project references to `NodeEditor.Blazor` (which transitively includes `NodeEditor.Net`):

```xml
<!-- MyNodeEditor.csproj -->
<ItemGroup>
  <ProjectReference Include="../NodeEditor.Blazor/NodeEditor.Blazor.csproj" />
</ItemGroup>
```

## Step 3: Register Services

In your `Program.cs`, register all node editor services with a single call:

```csharp
using NodeEditor.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register all node editor services (state, execution, registry, plugins, etc.)
builder.Services.AddNodeEditor();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
```

The `AddNodeEditor()` call registers all required services:

| Service | Lifetime | Purpose |
|---------|----------|---------|
| `NodeEditorState` | Scoped | Central state management with events |
| `NodeExecutionService` | Scoped | Executes node graphs |
| `GraphSerializer` | Scoped | JSON save/load |
| `NodeRegistryService` | Singleton | Manages available node definitions |
| `PluginLoader` | Singleton | Loads plugins from disk |
| `SocketTypeResolver` | Singleton | Maps type names to .NET types |
| `ConnectionValidator` | Scoped | Validates socket connections |
| `CoordinateConverter` | Scoped | Screen ↔ graph coordinate conversion |
| `ViewportCuller` | Scoped | Performance optimization for large graphs |
| `INodeEditorLogger` | Singleton | Channel-based logging |
| `IPluginEventBus` | Scoped | Plugin event subscriptions |

## Step 4: Add the Node Editor Canvas

Create a Razor page with the node editor canvas:

```razor
@page "/editor"
@using NodeEditor.Blazor.Components
@using NodeEditor.Net.Services
@using NodeEditor.Net.Services.Registry
@inject NodeEditorState EditorState
@inject INodeRegistryService Registry

<PageTitle>Node Editor</PageTitle>

<div style="width: 100%; height: 100vh; position: relative;">
    <NodeEditorCanvas State="@EditorState" />
</div>

@code {
    protected override void OnInitialized()
    {
        // Register nodes from this assembly so they appear in the context menu
        Registry.RegisterFromAssembly(typeof(AddNode).Assembly);
    }
}
```

## Step 5: Define Your First Nodes

Create node classes by subclassing `NodeBase`. Each node overrides `Configure` (to declare metadata and sockets via the fluent builder API) and `ExecuteAsync` (to run logic):

```csharp
using NodeEditor.Net.Services.Execution;

namespace MyNodeEditor;

// A data node — computes a value from inputs (no execution flow sockets)
public sealed class AddNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add").Category("Math")
            .Description("Adds two numbers.")
            .Input<double>("A", 0.0)
            .Input<double>("B", 0.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var a = context.GetInput<double>("A");
        var b = context.GetInput<double>("B");
        context.SetOutput("Result", a + b);
        return Task.CompletedTask;
    }
}

// A callable node — has execution flow sockets (Enter/Exit)
public sealed class PrintNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Print").Category("Debug")
            .Description("Prints a message to the debug output.")
            .Callable()
            .Input<string>("Message", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var message = context.GetInput<string>("Message");
        context.EmitFeedback(message, ExecutionFeedbackType.DebugPrint);
        await context.TriggerAsync("Exit");
    }
}

// An execution initiator — starts the execution chain
public sealed class MyStartNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("My Start").Category("Flow")
            .Description("Entry point for execution.")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
```

## Step 6: Run Your App

```bash
dotnet run
```

Open the browser and navigate to `/editor`. You should see:
- A dark canvas that you can **pan** (middle-click drag) and **zoom** (scroll wheel)
- **Right-click** to open the context menu and add nodes
- **Drag from a socket** to create connections between nodes
- **Click a node** to select it; press **Delete** to remove it

## Understanding Node Types

### Data Nodes (no execution sockets)

Data nodes compute outputs from inputs. They have no execution flow sockets and evaluate lazily when their outputs are needed. Omit `Callable()` / `ExecutionInitiator()` in the builder.

```csharp
public sealed class MultiplyNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Multiply").Category("Math")
            .Input<double>("A", 0.0)
            .Input<double>("B", 0.0)
            .Output<double>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<double>("A") * context.GetInput<double>("B"));
        return Task.CompletedTask;
    }
}
```

### Callable Nodes

Callable nodes participate in execution flow. Call `Callable()` on the builder to add `Enter` and `Exit` execution sockets automatically. Trigger the next node with `context.TriggerAsync("Exit")`.

```csharp
public sealed class LogNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Log").Category("Debug")
            .Description("Logs text to debug output.")
            .Callable()
            .Input<string>("Text", "");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var text = context.GetInput<string>("Text");
        context.EmitFeedback(text, ExecutionFeedbackType.DebugPrint);
        await context.TriggerAsync("Exit");
    }
}
```

### Execution Initiators

These nodes start an execution chain. Call `ExecutionInitiator()` — this adds an `Exit` execution output but no execution input.

```csharp
public sealed class OnStartNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("On Start").Category("Events")
            .Description("Entry point for execution.")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}
```

### Branching Nodes

Create conditional execution by using named execution outputs with `ExecutionInput` / `ExecutionOutput` and triggering different paths:

```csharp
public sealed class BranchNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Branch").Category("Conditions")
            .Description("Branch on a boolean condition.")
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

## Canvas Interactions

| Action | Description |
|--------|-------------|
| **Right-click** on canvas | Open context menu to add nodes |
| **Left-click + drag** on canvas | Box selection |
| **Left-click + drag** on a node | Move node (or all selected nodes) |
| **Left-click + drag** from a socket | Create a connection |
| **Middle-click + drag** | Pan the canvas |
| **Scroll wheel** | Zoom in/out |
| **Delete / Backspace** | Delete selected nodes |
| **Ctrl+A** | Select all nodes |
| **Escape** | Cancel current operation |

## Saving and Loading Graphs

```csharp
@inject GraphSerializer Serializer

// Save
var dto = Serializer.Export(EditorState);
var json = Serializer.Serialize(dto);
File.WriteAllText("my-graph.json", json);

// Load
var json = File.ReadAllText("my-graph.json");
var dto = Serializer.Deserialize(json);
var result = Serializer.Import(EditorState, dto);

foreach (var warning in result.Warnings)
    Console.WriteLine($"Warning: {warning}");
```

## Using Socket Editors

Control how socket values are edited by passing a `SocketEditorHint` to the builder's `Input` call:

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

public sealed class ImageLoaderNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Image Loader").Category("Media")
            .Description("Load an image file.")
            .Callable()
            .Input<string>("ImagePath", "",
                editorHint: new SocketEditorHint(SocketEditorKind.Image, Label: "Image Path"))
            .Input<string>("Format", "PNG",
                editorHint: new SocketEditorHint(SocketEditorKind.Dropdown, Options: "PNG,JPEG,BMP"))
            .Input<int>("Quality", 80,
                editorHint: new SocketEditorHint(SocketEditorKind.NumberUpDown, Min: 0, Max: 100, Step: 1));
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        // ...
        await context.TriggerAsync("Exit");
    }
}
```

Available editor kinds: `Text`, `Number`, `Bool`, `Dropdown`, `Button`, `Image`, `NumberUpDown`, `TextArea`, `Custom`.

## Next Steps

- **[Features Overview](FEATURES.md)** — Learn about all the capabilities of NodeEditor.Net
- **[Custom Nodes Tutorial](../NodeEditor.Blazor/docs/CUSTOM-NODES.md)** — Advanced node creation patterns
- **[Plugin SDK](reference/PLUGIN_SDK.md)** — Package your nodes as a reusable plugin
- **[Execution Engine](reference/EXECUTION_ENGINE.md)** — Understand how graphs are planned and executed
- **[MCP Integration](../NodeEditor.Mcp/README.md)** — Let AI assistants build graphs for you
