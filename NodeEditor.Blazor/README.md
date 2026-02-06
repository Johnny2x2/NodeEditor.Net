# NodeEditor.Blazor

A powerful, event-driven node editor component library for Blazor applications, supporting interactive graph-based visual programming with node execution, plugin extensibility, and serialization.

## Overview

NodeEditor.Blazor is a modern Blazor component library that provides a complete node-based editor solution. It enables users to create, connect, and execute nodes visually within a browser-based canvas. The library features an event-based architecture optimized for Blazor's reactive rendering model, plugin support for extensibility, and a robust execution engine for running node graphs.

### Key Features

- **Interactive Canvas** - Pan, zoom, drag nodes, and create connections with mouse/pointer interactions
- **Event-Based Architecture** - Reactive state management with fine-grained event subscriptions
- **Node Execution Engine** - Execute node graphs with parallel or sequential execution modes
- **Plugin System** - Dynamically load and register custom node types from external assemblies
- **Serialization** - Save and load node graphs with version migration support
- **Custom Editors** - Built-in socket value editors (text, numeric, boolean) with extensibility
- **Type System** - Type-safe socket connections with automatic type resolution
- **MVVM Pattern** - Clean separation between models, view models, and UI components
- **Comprehensive Testing** - Unit tests covering core functionality

## Getting Started

### Installation

Add the NodeEditor.Blazor package to your Blazor project:

```csharp
// In your Program.cs or Startup.cs
builder.Services.AddNodeEditor();
```

### Basic Usage

1. **Register services** in your Program.cs:

```csharp
using NodeEditor.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Register all node editor services
builder.Services.AddNodeEditor();

var app = builder.Build();
```

2. **Add the canvas component** to your Razor page:

```razor
@page "/editor"
@using NodeEditor.Blazor.Components
@using NodeEditor.Blazor.Services
@inject NodeEditorState EditorState

<NodeEditorCanvas State="@EditorState" />
```

3. **Initialize nodes programmatically**:

```csharp
@code {
    protected override void OnInitialized()
    {
        // Create a simple node
        var nodeData = new NodeData(
            Id: Guid.NewGuid().ToString(),
            Name: "My Node",
            Callable: false,
            ExecInit: false,
            Inputs: new List<SocketData>
            {
                new SocketData("Input", "double", false, false, new SocketValue(0.0))
            },
            Outputs: new List<SocketData>
            {
                new SocketData("Output", "double", false, true, new SocketValue(0.0))
            }
        );

        var viewModel = new NodeViewModel(nodeData)
        {
            Position = new Point2D(100, 100)
        };

        EditorState.AddNode(viewModel);
    }
}
```

## Architecture

### Event-Based State Management

NodeEditor.Blazor uses an event-driven architecture centered around `NodeEditorState`:

```csharp
public sealed class NodeEditorState
{
    // Events for reactive updates
    public event EventHandler<NodeEventArgs>? NodeAdded;
    public event EventHandler<NodeEventArgs>? NodeRemoved;
    public event EventHandler<ConnectionEventArgs>? ConnectionAdded;
    public event EventHandler<ConnectionEventArgs>? ConnectionRemoved;
    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<ViewportChangedEventArgs>? ViewportChanged;
    public event EventHandler<ZoomChangedEventArgs>? ZoomChanged;

    // Collections
    public ObservableCollection<NodeViewModel> Nodes { get; }
    public ObservableCollection<ConnectionData> Connections { get; }
    public HashSet<string> SelectedNodeIds { get; }

    // Properties
    public double Zoom { get; set; }
    public Rect2D Viewport { get; set; }
}
```

Components subscribe to specific state changes for efficient rendering:

```csharp
protected override void OnInitialized()
{
    EditorState.NodeAdded += OnNodeAdded;
    EditorState.SelectionChanged += OnSelectionChanged;
}

public void Dispose()
{
    EditorState.NodeAdded -= OnNodeAdded;
    EditorState.SelectionChanged -= OnSelectionChanged;
}
```

### Component Hierarchy

```
NodeEditorCanvas (Main canvas component)
├── ContextMenu (Right-click menu for adding nodes)
├── ConnectionPath (SVG connections between nodes)
└── NodeComponent (Individual node rendering)
    ├── SocketComponent (Input/output sockets)
    └── Custom Editors (Inline value editors)
```

### Core Services

- **NodeEditorState** - Central state management with event notifications
- **NodeExecutionService** - Executes node graphs with dependency resolution
- **NodeRegistryService** - Manages available node definitions
- **PluginLoader** - Loads and registers plugin assemblies
- **GraphSerializer** - Serializes/deserializes node graphs to/from JSON
- **ConnectionValidator** - Validates socket type compatibility
- **SocketTypeResolver** - Resolves socket types for method invocation
- **CoordinateConverter** - Converts between screen and graph coordinates

## Core Concepts

### Nodes

Nodes are the fundamental building blocks. Each node has:
- **Unique ID** - Identifies the node instance
- **Name** - Display name
- **Callable** - Whether the node can be executed
- **ExecInit** - Whether the node is an execution entry point
- **Inputs** - List of input sockets
- **Outputs** - List of output sockets

Nodes are defined using the `[Node]` attribute:

```csharp
public class MathContext : INodeContext
{
    [Node("Add", category: "Math", description: "Add two numbers", isCallable: false)]
    public void Add(double A, double B, out double Result)
    {
        Result = A + B;
    }
}
```

### Sockets

Sockets are connection points on nodes:
- **Input Sockets** - Receive data or execution flow
- **Output Sockets** - Send data or execution flow
- **Execution Sockets** - Control flow (white)
- **Data Sockets** - Value flow (colored by type)

Socket types are automatically inferred from method parameters and return values.

### Connections

Connections link output sockets to input sockets:
- **Type Validation** - Connections are type-checked
- **Execution vs Data** - Execution connections control flow, data connections transfer values
- **Single Input Rule** - Each input socket accepts only one connection

### State Management

The `NodeEditorState` class provides:
- **Centralized State** - Single source of truth
- **Event Notifications** - Components subscribe to changes
- **Selection Management** - Track selected nodes
- **Viewport Management** - Pan and zoom state

## Features

### Node Execution

Execute node graphs with configurable options:

```csharp
var service = serviceProvider.GetRequiredService<NodeExecutionService>();
var context = new NodeExecutionContext();
var nodeContext = new StandardNodeContext();

var options = new NodeExecutionOptions
{
    Mode = ExecutionMode.Parallel,
    MaxDegreeOfParallelism = 4
};

await service.ExecuteAsync(
    nodes: EditorState.Nodes.Select(n => n.Data).ToList(),
    connections: EditorState.Connections.ToList(),
    context: context,
    nodeContext: nodeContext,
    options: options,
    token: cancellationToken
);
```

**Execution Modes:**
- **Parallel** - Execute independent nodes concurrently
- **Sequential** - Execute nodes one at a time following execution paths

**Execution Events:**
```csharp
service.NodeStarted += (sender, e) => Console.WriteLine($"Started: {e.Node.Name}");
service.NodeCompleted += (sender, e) => Console.WriteLine($"Completed: {e.Node.Name}");
service.ExecutionFailed += (sender, e) => Console.WriteLine($"Error: {e.Message}");
```

### Plugin System

Create custom node plugins by implementing `INodePlugin`:

```csharp
public sealed class MyPlugin : INodePlugin
{
    public string Name => "My Custom Nodes";
    public string Id => "com.example.myplugin";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(MyPlugin).Assembly);
    }
}

public class MyPluginContext : INodeContext
{
    [Node("Custom Node", category: "Custom", description: "My custom node")]
    public void CustomNode(int Input, out int Output)
    {
        Output = Input * 2;
    }
}
```

**Plugin Discovery:**
```csharp
// In Program.cs
var pluginLoader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
await pluginLoader.LoadAndRegisterAsync("./plugins");
```

**Plugin Structure:**
```
plugins/
├── MyPlugin/
│   ├── plugin.json (optional manifest)
│   └── MyPlugin.dll
```

### Serialization

Save and load node graphs:

```csharp
var serializer = serviceProvider.GetRequiredService<GraphSerializer>();

// Export to DTO
var dto = serializer.Export(EditorState);

// Serialize to JSON
var json = serializer.Serialize(dto);

// Deserialize from JSON
var loadedDto = serializer.Deserialize(json);

// Import into state
var result = serializer.Import(EditorState, loadedDto);

// Check for warnings
foreach (var warning in result.Warnings)
{
    Console.WriteLine(warning);
}
```

**Graph Format:**
```json
{
  "version": 1,
  "nodes": [
    {
      "id": "node-1",
      "typeId": "Math.Add",
      "name": "Add",
      "callable": false,
      "execInit": false,
      "x": 100,
      "y": 200,
      "width": 140,
      "height": 60,
      "inputs": [...],
      "outputs": [...]
    }
  ],
  "connections": [
    {
      "outputNodeId": "node-1",
      "outputSocketName": "Result",
      "inputNodeId": "node-2",
      "inputSocketName": "A",
      "isExecution": false
    }
  ],
  "viewport": {
    "x": 0,
    "y": 0,
    "width": 1920,
    "height": 1080,
    "zoom": 1.0
  },
  "selectedNodeIds": []
}
```

### Custom Editors

Create custom socket value editors:

```csharp
public sealed class ColorEditorDefinition : INodeCustomEditor
{
    public string TypeName => "Color";
    public int Priority => 100;

    public bool CanEdit(SocketData socket)
    {
        return socket.TypeName == "Color" && socket.IsInput;
    }

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "color");
            builder.AddAttribute(2, "value", context.Socket.Value?.Value?.ToString() ?? "#000000");
            builder.AddAttribute(3, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this, e => context.SetValue(e.Value?.ToString() ?? "#000000")));
            builder.CloseElement();
        };
    }
}

// Register in DI
services.AddSingleton<INodeCustomEditor, ColorEditorDefinition>();
```

## API Reference

### NodeEditorState

Central state management class.

**Methods:**
- `AddNode(NodeViewModel)` - Add a node
- `RemoveNode(string nodeId)` - Remove a node
- `AddConnection(ConnectionData)` - Add a connection
- `RemoveConnection(ConnectionData)` - Remove a connection
- `SelectNode(string nodeId, bool clearExisting)` - Select a node
- `SelectNodes(IEnumerable<string>, bool clearExisting)` - Select multiple nodes
- `ClearSelection()` - Clear all selections
- `RemoveSelectedNodes()` - Delete selected nodes
- `Clear()` - Clear all nodes and connections

**Properties:**
- `Nodes` - Observable collection of nodes
- `Connections` - Observable collection of connections
- `SelectedNodeIds` - Set of selected node IDs
- `Zoom` - Current zoom level
- `Viewport` - Current viewport rectangle

### NodeEditorCanvas

Main canvas component for rendering and interacting with the node graph.

**Parameters:**
- `State` (required) - The NodeEditorState instance
- `MinZoom` - Minimum zoom level (default: 0.1)
- `MaxZoom` - Maximum zoom level (default: 3.0)
- `ZoomStep` - Zoom increment (default: 0.1)

**Interactions:**
- Left-click + drag: Box selection
- Middle-click + drag: Pan canvas
- Mouse wheel: Zoom in/out
- Right-click: Open context menu
- Delete/Backspace: Delete selected nodes
- Ctrl+A: Select all nodes
- Escape: Cancel current operation

### NodeExecutionService

Executes node graphs with dependency resolution.

**Methods:**
- `ExecuteAsync(nodes, connections, context, nodeContext, options, token)` - Execute a graph
- `ExecuteGroupAsync(group, parentContext, nodeContext, options, token)` - Execute a subgraph

**Events:**
- `NodeStarted` - Fired when a node begins execution
- `NodeCompleted` - Fired when a node completes execution
- `ExecutionFailed` - Fired when execution fails
- `ExecutionCanceled` - Fired when execution is canceled
- `LayerStarted` - Fired when an execution layer begins
- `LayerCompleted` - Fired when an execution layer completes

### NodeRegistryService

Manages available node definitions.

**Methods:**
- `EnsureInitialized(assemblies?)` - Initialize the registry
- `RegisterFromAssembly(Assembly)` - Register nodes from an assembly
- `RegisterDefinitions(IEnumerable<NodeDefinition>)` - Register node definitions
- `GetCatalog(string? search)` - Get a catalog of available nodes

**Properties:**
- `Definitions` - Read-only list of all registered node definitions

**Events:**
- `RegistryChanged` - Fired when the registry is updated

## Project Structure

```
NodeEditor.Blazor/
├── Adapters/              # Legacy adapter code
│   └── NodeAdapter.cs
├── Components/            # Blazor UI components
│   ├── ConnectionPath.razor
│   ├── ContextMenu.razor
│   ├── NodeComponent.razor
│   ├── NodeEditorCanvas.razor
│   ├── SocketComponent.razor
│   └── Editors/           # Socket value editors
│       ├── BoolEditor.razor
│       ├── NumericEditor.razor
│       └── TextEditor.razor
├── Models/                # Data models
│   ├── NodeData.cs
│   ├── SocketData.cs
│   ├── ConnectionData.cs
│   ├── GraphDto.cs
│   ├── Point2D.cs
│   ├── Size2D.cs
│   └── ...
├── Services/              # Core services
│   ├── NodeEditorState.cs
│   ├── ConnectionValidator.cs
│   ├── CoordinateConverter.cs
│   ├── SocketTypeResolver.cs
│   ├── Editors/           # Custom editor system
│   │   ├── INodeCustomEditor.cs
│   │   └── NodeEditorCustomEditorRegistry.cs
│   ├── Execution/         # Execution engine
│   │   ├── NodeExecutionService.cs
│   │   ├── ExecutionPlanner.cs
│   │   ├── NodeMethodInvoker.cs
│   │   └── ...
│   ├── Plugins/           # Plugin system
│   │   ├── PluginLoader.cs
│   │   ├── INodePlugin.cs
│   │   └── ...
│   ├── Registry/          # Node registry
│   │   ├── NodeRegistryService.cs
│   │   ├── NodeDiscoveryService.cs
│   │   └── ...
│   └── Serialization/     # Graph serialization
│       ├── GraphSerializer.cs
│       └── GraphSchemaMigrator.cs
├── ViewModels/            # MVVM view models
│   ├── NodeViewModel.cs
│   └── SocketViewModel.cs
└── wwwroot/               # Static assets
    ├── css/
    └── exampleJsInterop.js
```

## Testing

The project includes comprehensive unit tests in `NodeEditor.Blazor.Tests`:

- **NodeEditorStateTests** - State management and events
- **ExecutionEngineTests** - Node execution and planning
- **GraphSerializerTests** - Serialization and deserialization
- **NodeRegistryTests** - Node discovery and registration
- **PluginLoaderTests** - Plugin loading and validation
- **ConnectionValidatorTests** - Type compatibility validation
- **SocketTypeResolverTests** - Type resolution
- **ComponentTests** - Blazor component rendering

## Examples

### Example 1: Math Calculator

```csharp
public class MathContext : INodeContext
{
    [Node("Constant", category: "Math", description: "Constant value")]
    public void Constant(out double Value)
    {
        Value = 42.0;
    }

    [Node("Add", category: "Math", description: "Add two numbers")]
    public void Add(double A, double B, out double Result)
    {
        Result = A + B;
    }

    [Node("Multiply", category: "Math", description: "Multiply two numbers")]
    public void Multiply(double A, double B, out double Result)
    {
        Result = A * B;
    }

    [Node("Log Result", category: "Debug", description: "Log value to console", isCallable: true)]
    public void LogResult(ExecutionPath Entry, double Value, out ExecutionPath Exit)
    {
        Console.WriteLine($"Result: {Value}");
        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
```

### Example 2: Conditional Flow

```csharp
public class FlowContext : INodeContext
{
    [Node("Branch", category: "Flow", description: "Conditional branch", isCallable: true)]
    public void Branch(ExecutionPath Entry, bool Condition, out ExecutionPath True, out ExecutionPath False)
    {
        True = new ExecutionPath();
        False = new ExecutionPath();

        if (Condition)
            True.Signal();
        else
            False.Signal();
    }

    [Node("Compare", category: "Math", description: "Compare two numbers")]
    public void Compare(double A, double B, out bool Greater, out bool Equal, out bool Less)
    {
        Greater = A > B;
        Equal = Math.Abs(A - B) < 0.0001;
        Less = A < B;
    }
}
```

### Example 3: Custom Node Provider

```csharp
public class StringPlugin : INodePlugin, INodeProvider
{
    public string Name => "String Operations";
    public string Id => "com.example.strings";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterDefinitions(GetNodeDefinitions());
    }

    public IEnumerable<NodeDefinition> GetNodeDefinitions()
    {
        yield return new NodeDefinition(
            Id: "String.Concat",
            Name: "Concat",
            Category: "String",
            Description: "Concatenate strings",
            Factory: () => new NodeData(
                Guid.NewGuid().ToString(),
                "Concat",
                false,
                false,
                new List<SocketData>
                {
                    new("A", "string", false, false, new SocketValue("")),
                    new("B", "string", false, false, new SocketValue(""))
                },
                new List<SocketData>
                {
                    new("Result", "string", false, true, new SocketValue(""))
                }
            )
        );
    }
}
```

## Requirements

- **.NET 10.0** or later
- **Blazor Server** or **Blazor WebAssembly**
- **Microsoft.AspNetCore.Components.Web 10.0+**

## Browser Support

- Chrome/Edge (Chromium) 90+
- Firefox 88+
- Safari 14+

## Contributing

Contributions are welcome! The project follows standard C# coding conventions and includes:
- XML documentation for public APIs
- Unit tests for core functionality
- Event-based reactive patterns

## License

See LICENSE.txt in the repository root.

## Related Projects

- **NodeEditor** - Original WinForms-based node editor
- **NodeEditorMax** - MAUI cross-platform implementation
- **NodeEditor.Blazor.WebHost** - Example Blazor Server hosting application
