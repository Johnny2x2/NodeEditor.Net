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
@inject NodeRegistryService Registry

<PageTitle>Node Editor</PageTitle>

<div style="width: 100%; height: 100vh; position: relative;">
    <NodeEditorCanvas State="@EditorState" />
</div>

@code {
    protected override void OnInitialized()
    {
        // Register nodes from this assembly so they appear in the context menu
        Registry.EnsureInitialized(new[] { typeof(MyNodes).Assembly });
    }
}
```

## Step 5: Define Your First Nodes

Create a node context class with `[Node]`-attributed methods:

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;

namespace MyNodeEditor;

public sealed class MyNodes : INodeContext
{
    // A data node (no execution flow) — computes a value from inputs
    [Node("Add", category: "Math", description: "Adds two numbers", isCallable: false)]
    public void Add(double A, double B, out double Result)
    {
        Result = A + B;
    }

    // An executable node — has execution flow sockets (Entry/Exit)
    [Node("Print", category: "Debug", description: "Prints a value", isCallable: true)]
    public void Print(ExecutionPath Entry, string Message, out ExecutionPath Exit)
    {
        Console.WriteLine(Message);
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    // An execution initiator — starts the execution chain
    [Node("Start", category: "Flow", description: "Entry point",
          isCallable: true, isExecutionInitiator: true)]
    public void Start(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
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

### Data Nodes (`isCallable: false`)

Data nodes compute outputs from inputs. They have no execution flow sockets and evaluate lazily when their outputs are needed.

```csharp
[Node("Multiply", category: "Math", isCallable: false)]
public void Multiply(double A, double B, out double Result)
{
    Result = A * B;
}
```

### Callable Nodes (`isCallable: true`)

Callable nodes participate in execution flow. They have `ExecutionPath` input/output sockets that control when they run.

```csharp
[Node("Log", category: "Debug", isCallable: true)]
public void Log(ExecutionPath Entry, string Text, out ExecutionPath Exit)
{
    Console.WriteLine(Text);
    Exit = new ExecutionPath();
    Exit.Signal();
}
```

### Execution Initiators (`isExecutionInitiator: true`)

These nodes start an execution chain. They have no execution input but produce execution outputs.

```csharp
[Node("On Start", category: "Events", isCallable: true, isExecutionInitiator: true)]
public void OnStart(out ExecutionPath Exit)
{
    Exit = new ExecutionPath();
    Exit.Signal();
}
```

### Branching Nodes

Create conditional execution by signaling different execution paths:

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

Control how socket values are edited using the `[SocketEditor]` attribute:

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

[Node("Image Loader", category: "Media", isCallable: true)]
public void LoadImage(
    ExecutionPath Entry,
    [SocketEditor(SocketEditorKind.Image, Label = "Image Path")] string ImagePath,
    [SocketEditor(SocketEditorKind.Dropdown, Options = "PNG,JPEG,BMP")] string Format,
    [SocketEditor(SocketEditorKind.NumberUpDown, Min = 0, Max = 100, Step = 1)] int Quality,
    out ExecutionPath Exit)
{
    // ...
    Exit = new ExecutionPath();
    Exit.Signal();
}
```

Available editor kinds: `Text`, `Number`, `Bool`, `Dropdown`, `Button`, `Image`, `NumberUpDown`, `TextArea`, `Custom`.

## Next Steps

- **[Features Overview](FEATURES.md)** — Learn about all the capabilities of NodeEditor.Net
- **[Custom Nodes Tutorial](../NodeEditor.Blazor/docs/CUSTOM-NODES.md)** — Advanced node creation patterns
- **[Plugin SDK](reference/PLUGIN_SDK.md)** — Package your nodes as a reusable plugin
- **[Execution Engine](reference/EXECUTION_ENGINE.md)** — Understand how graphs are planned and executed
- **[MCP Integration](../NodeEditor.Mcp/README.md)** — Let AI assistants build graphs for you
