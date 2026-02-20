# NodeEditor.Net — Features Overview

This document provides a comprehensive guide to every major feature of NodeEditor.Net, explaining what it does, why it matters, and how to use it. It is intended as an information base for the wiki and for users who want to understand the full capabilities of the product.

---

## Table of Contents

1. [Interactive Canvas](#1-interactive-canvas)
2. [Node System](#2-node-system)
3. [Execution Engine](#3-execution-engine)
4. [Graph Variables](#4-graph-variables)
5. [Graph Events](#5-graph-events)
6. [Overlays](#6-overlays)
7. [Serialization](#7-serialization)
8. [Plugin System](#8-plugin-system)
9. [Plugin Marketplace](#9-plugin-marketplace)
10. [Custom Socket Editors](#10-custom-socket-editors)
11. [Type System](#11-type-system)
12. [MCP Integration](#12-mcp-integration)
13. [Headless Execution](#13-headless-execution)
14. [Logging System](#14-logging-system)
15. [Infrastructure Services](#15-infrastructure-services)
16. [Built-in Standard Nodes](#16-built-in-standard-nodes)

---

## 1. Interactive Canvas

### What It Is

The `NodeEditorCanvas` is the main Blazor component that renders the node graph. It provides a fully interactive workspace where users can create, connect, arrange, and execute nodes.

### Why It Matters

Visual programming depends on an intuitive, responsive canvas. The canvas handles all user interaction—mouse, keyboard, and touch—while maintaining smooth performance through viewport culling and selective re-rendering.

### How to Use It

```razor
@using NodeEditor.Blazor.Components
@using NodeEditor.Net.Services
@inject NodeEditorState EditorState

<NodeEditorCanvas State="@EditorState"
                  MinZoom="0.1"
                  MaxZoom="3.0"
                  ZoomStep="0.1" />
```

### Canvas Interactions

| Action | Description |
|--------|-------------|
| Right-click on canvas | Open context menu to add nodes |
| Left-click + drag on canvas | Box selection |
| Left-click + drag on a node | Move node (or all selected nodes) |
| Left-click + drag from socket | Create a connection |
| Middle-click + drag | Pan the canvas |
| Scroll wheel | Zoom in/out (centered on cursor) |
| Delete / Backspace | Remove selected nodes and their connections |
| Ctrl+A | Select all nodes |
| Escape | Cancel current operation |

### Component Hierarchy

```
NodeEditorCanvas
├── ContextMenu (right-click node picker with search)
├── SelectionRectangle (box selection overlay)
├── ConnectionPath (SVG curves between sockets)
├── ConnectionDrawing (pending connection while dragging)
├── OrganizerOverlay (organizer shapes)
├── NodeComponent (individual node rendering)
│   ├── SocketComponent (input/output socket rendering)
│   │   └── Editor (inline value editor for unconnected inputs)
│   └── Custom Editors (plugin-provided editors)
├── NodePropertiesPanel (property inspector)
├── VariablesPanel (graph variable management)
├── EventsPanel (graph event management)
├── OutputTerminalPanel (execution output display)
├── PluginManagerOverlay (plugin management)
└── McpSettingsPanel (MCP configuration)
```

---

## 2. Node System

### What It Is

Nodes are the fundamental building blocks of a graph. Each node encapsulates a small operation—adding numbers, formatting text, branching on a condition—and exposes input and output **sockets** for data and execution flow.

### How Nodes Are Defined

Nodes are defined by subclassing `NodeBase`, overriding `Configure(INodeBuilder)` for metadata/sockets and `ExecuteAsync(INodeExecutionContext, CancellationToken)` for logic:

```csharp
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

public sealed class AddNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add")
               .Category("Math")
               .Description("Add two numbers")
               .Input<double>("A", 0.0)
               .Input<double>("B", 0.0)
               .Output<double>("Result");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result",
            context.GetInput<double>("A") + context.GetInput<double>("B"));
        return Task.CompletedTask;
    }
}
```

### Builder API

| Method | Description |
|--------|-------------|
| `Name(string)` | Display name in the editor |
| `Category(string)` | Grouping category (supports nesting: `"Math/Trig"`) |
| `Description(string)` | Tooltip text |
| `Input<T>(name, default?, editorHint?)` | Add an input socket |
| `Output<T>(name)` | Add an output socket |
| `Callable()` | Add Enter/Exit execution sockets |
| `ExecutionInitiator()` | Add Exit socket only (starts execution chain) |
| `ExecutionInput(name)` | Named execution input |
| `ExecutionOutput(name)` | Named execution output |

### Socket Types

- **`Input<T>()`** → Input sockets (left side of node)
- **`Output<T>()`** → Output sockets (right side of node)
- **`.Callable()` / `.ExecutionInitiator()`** → Execution flow sockets (control when the node runs)

### Node Discovery and Registration

Nodes are discovered automatically through assembly scanning:

```csharp
// Register all [Node] methods from an assembly
Registry.RegisterFromAssembly(typeof(MathContext).Assembly);

// Or initialize with multiple assemblies
Registry.EnsureInitialized(new[] { assembly1, assembly2 });
```

### Connection Rules

- Each input socket accepts at most one incoming connection
- Output sockets can connect to multiple inputs
- Execution sockets can only connect to other execution sockets
- Data sockets are type-checked for compatibility

---

## 3. Execution Engine

### What It Is

The execution engine takes a visual node graph and runs it. It handles dependency resolution, topological sorting, input propagation, and output collection.

### Execution Modes

| Mode | Description | Use Case |
|------|-------------|----------|
| **Sequential** | Follows execution paths through callable nodes | Imperative workflows with control flow |
| **Parallel** | Runs independent data nodes concurrently | Data pipelines where order doesn't matter |
| **Event-driven** | Triggered by custom graph events | Reactive systems |

### How to Execute a Graph

```csharp
using NodeEditor.Net.Services.Execution;

var executionService = serviceProvider.GetRequiredService<NodeExecutionService>();
var context = new NodeExecutionContext();
var nodeContext = new CompositeNodeContext(new StandardNodeContext(), new MyNodes());

var options = new NodeExecutionOptions
{
    Mode = ExecutionMode.Parallel,
    MaxDegreeOfParallelism = 4
};

await executionService.ExecuteAsync(
    nodes: editorState.BuildExecutionNodes(),
    connections: editorState.Connections.ToList(),
    context: context,
    nodeContext: nodeContext,
    options: options,
    token: cancellationToken
);

// Apply results back to the UI
editorState.ApplyExecutionContext(context);
```

### Execution Events

| Event | Description |
|-------|-------------|
| `NodeStarted` | Fired when a node begins execution |
| `NodeCompleted` | Fired when a node finishes successfully |
| `ExecutionFailed` | Fired when execution encounters an error |
| `LayerStarted` | Fired when a parallel layer begins |
| `LayerCompleted` | Fired when a parallel layer completes |

For a deep dive, see the [Execution Engine Reference](reference/EXECUTION_ENGINE.md).

---

## 4. Graph Variables

### What It Is

Graph variables are named, graph-scoped values that can be read and written by nodes during execution. When you create a variable, the system automatically generates **Get** and **Set** nodes for it.

### Why It Matters

Variables let you share state across distant parts of your graph without threading long connection wires. They're essential for counters, accumulators, configuration values, and any data that multiple nodes need to access.

### How to Use

1. **Create a variable** through the Variables Panel or programmatically:
   ```csharp
   editorState.AddVariable(new GraphVariable
   {
       Id = Guid.NewGuid().ToString(),
       Name = "Score",
       TypeName = "double",
       DefaultValue = new SocketValue(0.0)
   });
   ```

2. **Auto-generated nodes** appear in the context menu:
   - **Get Score** — outputs the current value of the variable
   - **Set Score** — sets the variable to a new value

3. **During execution**, variables are seeded from their default values via `VariableNodeExecutor.SeedVariables()`, and Get/Set nodes read and write the shared execution context.

For details, see [Variables & Events Reference](reference/VARIABLES_AND_EVENTS.md).

---

## 5. Graph Events

### What It Is

Graph events provide an event-driven execution model. When you create an event, the system generates two node types:
- **Trigger Event** — fires the event with optional payload data
- **Custom Event (Listener)** — starts execution when the event is fired

### Why It Matters

Events decouple parts of your graph. Instead of hard-wiring execution paths, you can trigger events from one section and handle them in another, enabling modular, reactive graph designs.

### How to Use

1. **Create an event** through the Events Panel or programmatically
2. **Place a Trigger node** where you want to fire the event
3. **Place a Listener node** where you want to react to the event
4. When the trigger fires during execution, all connected listeners begin their execution chains

For details, see [Variables & Events Reference](reference/VARIABLES_AND_EVENTS.md).

---

## 6. Overlays

### What It Is

Overlays are non-functional visual shapes that sit behind nodes on the canvas. They help you organize and annotate your graph by grouping related nodes into labeled regions.

### Why It Matters

Large graphs can become difficult to navigate. Overlays act as visual "folders" or "regions" that make graph structure clear at a glance.

### Properties

| Property | Description |
|----------|-------------|
| `Title` | Header text displayed at the top |
| `Body` | Optional description text |
| `Position` | Location on the canvas |
| `Size` | Width and height of the region |
| `Color` | Background color |
| `Opacity` | Transparency level |

### How to Use

Overlays can be created through the UI's organizer tool or programmatically:

```csharp
editorState.AddOverlay(new OverlayViewModel
{
    Id = Guid.NewGuid().ToString(),
    Title = "Input Processing",
    Body = "These nodes handle user input",
    Position = new Point2D(100, 100),
    Size = new Size2D(400, 300),
    Color = new ColorValue(50, 100, 200),
    Opacity = 0.15
});
```

Overlays are serialized as part of the graph and restored on load.

For details, see [Overlays Reference](reference/OVERLAYS.md).

---

## 7. Serialization

### What It Is

The `GraphSerializer` service saves and loads entire node graphs as JSON, including nodes, connections, socket values, variables, events, overlays, viewport position, and zoom level.

### Why It Matters

Users need to save their work, share graphs with others, and version-control their visual programs. The serializer also supports schema migration so that graphs saved with older versions of the editor can be loaded in newer versions.

### How to Use

```csharp
using NodeEditor.Net.Services.Serialization;

@inject GraphSerializer Serializer

// Save to JSON
var dto = Serializer.Export(EditorState);
var json = Serializer.Serialize(dto);
File.WriteAllText("graph.json", json);

// Load from JSON
var json = File.ReadAllText("graph.json");
var dto = Serializer.Deserialize(json);
var result = Serializer.Import(EditorState, dto);

// Check for import warnings
foreach (var warning in result.Warnings)
    Console.WriteLine($"Warning: {warning}");
```

### Schema Migration

The `GraphSchemaMigrator` handles version differences automatically. When a graph saved with an older schema version is loaded, it's migrated to the current version transparently.

---

## 8. Plugin System

### What It Is

The plugin system lets you extend the node editor with custom node types packaged as external .NET assemblies. Plugins are loaded dynamically at runtime using isolated `AssemblyLoadContext` instances.

### Why It Matters

Plugins enable a modular ecosystem where domain-specific node types (image processing, audio, data science, game logic, etc.) can be developed, distributed, and installed independently.

### Plugin Interface

Plugins implement `INodePlugin` with these lifecycle hooks:

```csharp
public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }

    void Register(INodeRegistryService registry);
    void ConfigureServices(IServiceCollection services);
    Task OnLoadAsync(CancellationToken token);
    Task OnInitializeAsync(IServiceProvider services, CancellationToken token);
    Task OnUnloadAsync(CancellationToken token);
    void OnError(Exception exception);
    void Unload();
}
```

### Plugin Lifecycle

```
Assembly Loaded → OnLoadAsync() → ConfigureServices() → Register() → OnInitializeAsync()
    ↓ (plugin is active)
OnUnloadAsync() → Unload() → Assembly Unloaded
```

### Plugin Manifest (`plugin.json`)

```json
{
    "Id": "com.example.myplugin",
    "Name": "My Plugin",
    "Version": "1.0.0",
    "MinApiVersion": "1.0.0",
    "EntryAssembly": "MyPlugin.dll",
    "Category": "Utilities"
}
```

### Loading Plugins

```csharp
var pluginLoader = serviceProvider.GetRequiredService<PluginLoader>();
await pluginLoader.LoadAndRegisterAsync("./plugins");
```

For full details, see the [Plugin SDK](reference/PLUGIN_SDK.md) and [Plugin Customization Guide](reference/PLUGIN_CUSTOMIZATION.md).

---

## 9. Plugin Marketplace

### What It Is

The marketplace system lets users browse, search, install, update, and uninstall plugins from both local and remote repositories through a built-in UI.

### Why It Matters

A marketplace makes plugin discovery frictionless. Users don't need to manually copy DLLs—they can install plugins with one click.

### Components

| Component | Description |
|-----------|-------------|
| `PluginManagerDialog` | Main plugin browser with search and categories |
| `PluginCard` | Display card for each available plugin |
| `PluginDetailsPanel` | Detailed view with description, version, and actions |
| `PluginSearchBar` | Filter plugins by name, category, or tag |
| `MarketplaceSettingsPanel` | Configure marketplace sources |

### Marketplace Sources

| Source | Description |
|--------|-------------|
| `LocalPluginMarketplaceSource` | Scans a local directory for `plugin.json` manifests |
| `RemotePluginMarketplaceSource` | HTTP-based remote repository with token auth |
| `AggregatedPluginMarketplaceSource` | Combines multiple sources into one |

### Publishing Plugins

```powershell
# Publish a single plugin
./publish-plugin.ps1 -PluginProject path/to/YourPlugin.csproj

# Publish all plugins in the solution
./publish-all-plugins.ps1
```

For details, see [Marketplace Reference](reference/MARKETPLACE.md).

---

## 10. Custom Socket Editors

### What It Is

Socket editors are inline UI controls that appear on unconnected input sockets, letting users set default values directly on the node.

### Built-in Editors

| Editor | Kind | Description |
|--------|------|-------------|
| `TextEditor` | `Text` | Single-line text input |
| `NumericEditor` | `Number` | Numeric input field |
| `NumberUpDownEditor` | `NumberUpDown` | Numeric spinner with min/max/step |
| `BoolEditor` | `Bool` | Checkbox toggle |
| `DropdownEditor` | `Dropdown` | Select from predefined options |
| `ButtonEditor` | `Button` | Clickable button action |
| `TextAreaEditor` | `TextArea` | Multi-line text input |
| `ImageEditor` | `Image` | Image upload/path selector |
| `ListEditor` | `List` | Dynamic list item editor |

### Using `SocketEditorHint`

Pass `SocketEditorHint` via the builder's `Input` method:

```csharp
public class ConfigNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Config").Category("Settings")
            .Input<string>("Name", "",
                new SocketEditorHint(SocketEditorKind.Text, Placeholder: "Enter name"))
            .Input<int>("Count", 50,
                new SocketEditorHint(SocketEditorKind.NumberUpDown, Min: 0, Max: 100, Step: 5))
            .Input<string>("Priority", "Medium",
                new SocketEditorHint(SocketEditorKind.Dropdown, Options: "Low,Medium,High"))
            .Input<bool>("Enabled", true,
                new SocketEditorHint(SocketEditorKind.Bool))
            .Output<string>("Summary");
    }

    public override Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var name = context.GetInput<string>("Name");
        var count = context.GetInput<int>("Count");
        var priority = context.GetInput<string>("Priority");
        var enabled = context.GetInput<bool>("Enabled");
        context.SetOutput("Summary",
            $"{name}: {count} ({priority}) - {(enabled ? "ON" : "OFF")}");
        return Task.CompletedTask;
    }
}
```

### Creating Custom Editors

Implement `INodeCustomEditor` for completely custom socket UIs:

```csharp
public sealed class ColorEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
        => socket.TypeName == "Color" && socket.IsInput && !socket.IsExecution;

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            builder.OpenElement(0, "input");
            builder.AddAttribute(1, "type", "color");
            builder.AddAttribute(2, "value", context.Socket.Data.Value?.ToObject<string>() ?? "#000000");
            builder.AddAttribute(3, "onchange", EventCallback.Factory.Create<ChangeEventArgs>(
                this, e => context.SetValue(e.Value?.ToString())));
            builder.CloseElement();
        };
    }
}
```

---

## 11. Type System

### What It Is

The `SocketTypeResolver` maps type name strings (used in node definitions) to .NET CLR types. The `ConnectionValidator` uses these mappings to enforce type-safe connections.

### Why It Matters

Type safety prevents users from connecting incompatible sockets (e.g., wiring a `string` output to a `double` input), catching errors at design time rather than execution time.

### How It Works

1. **Standard types** (`int`, `double`, `string`, `bool`, `float`) are registered automatically
2. **Custom types** must be registered explicitly:
   ```csharp
   typeResolver.Register("Vector3", typeof(Vector3));
   typeResolver.Register(typeof(Vector3).FullName!, typeof(Vector3));
   ```
3. **Connection validation** checks:
   - Source must be an output, target must be an input
   - Both must be the same kind (execution or data)
   - Data types must be compatible (exact match or assignable)

---

## 12. MCP Integration

### What It Is

The Model Context Protocol (MCP) server exposes the node editor's capabilities to AI assistants like Claude and Cursor. It lets AI create nodes, build connections, execute graphs, and manage plugins programmatically.

### Why It Matters

MCP integration enables AI-assisted visual programming. Users can describe what they want in natural language, and an AI assistant builds the graph for them.

### Transport Modes

| Mode | Description |
|------|-------------|
| **Stdio** | Standalone process with its own in-memory state |
| **HTTP/SSE** | Embedded in the WebHost, controlling the live canvas in real-time |

### Ability Categories

| Category | Example Abilities |
|----------|-------------------|
| Catalog | List available nodes, search by category |
| Nodes | Add, remove, move, select, set socket values |
| Connections | Add, remove, list connections |
| Graph | Save, load, clear, import/export JSON |
| Execution | Run graph, check status, pause/resume |
| Plugins | List, install, uninstall, enable/disable |
| Logging | Read logs, list channels, clear entries |
| Overlays | Add, remove, list organizer shapes |

### Setup

In `appsettings.json`:
```json
{
  "Mcp": {
    "Enabled": true,
    "RoutePattern": "/mcp"
  }
}
```

For full details, see the [MCP README](../NodeEditor.Mcp/README.md) and [MCP State Bridge Reference](reference/MCP_STATE_BRIDGE.md).

---

## 13. Headless Execution

### What It Is

The `HeadlessGraphRunner` lets you execute node graphs without any UI. It loads a graph from JSON or `GraphData` objects and runs it in a console app, API, background service, or any .NET host.

### Why It Matters

Headless execution enables:
- **CI/CD pipelines** — Run data processing graphs as part of build automation
- **API endpoints** — Execute graphs in response to HTTP requests
- **Background services** — Schedule graph execution on a timer
- **Testing** — Validate graph behavior in unit tests
- **MCP** — The MCP server's `execution.run_json` ability uses headless execution

### How to Use

```csharp
using NodeEditor.Net.Services.Execution;

var runner = serviceProvider.GetRequiredService<HeadlessGraphRunner>();

// Execute from JSON
var json = File.ReadAllText("my-graph.json");
var result = await runner.ExecuteFromJsonAsync(json, cancellationToken);

// Execute from GraphData
var graphData = serializer.Deserialize(json);
var result = await runner.ExecuteAsync(graphData, new NodeExecutionOptions
{
    Mode = ExecutionMode.Sequential
}, cancellationToken);
```

For details, see [Headless Execution Reference](reference/HEADLESS_EXECUTION.md).

---

## 14. Logging System

### What It Is

NodeEditor.Net includes a structured, channel-based logging system. Log messages are organized into named channels (Execution, Plugins, Serialization, etc.) with configurable retention policies.

### Why It Matters

When debugging complex graphs with multiple plugins, channel-based logging lets you filter noise and focus on the subsystem you care about. The output terminal panel displays logs in the UI.

### Key Components

| Component | Description |
|-----------|-------------|
| `INodeEditorLogger` | Central logger interface |
| `ILogChannelRegistry` | Registers and manages named channels |
| `LogChannels` | Pre-defined channel constants |
| `LogEntry` | Individual log message with level and channel |
| `ChannelClearPolicy` | Controls auto-clearing (`Manual`, `OnExecution`, etc.) |

### Pre-defined Channels

Channels for: Execution, Plugins, Serialization, and any custom channels registered by plugins.

### Plugin Log Channels

Plugins implementing `ILogChannelAware` can register their own named channels:

```csharp
public sealed class MyPlugin : INodePlugin, ILogChannelAware
{
    public void RegisterChannels(ILogChannelRegistry registry)
    {
        registry.RegisterChannel("My Plugin", pluginId: Id);
    }
}
```

For details, see [Logging Reference](reference/LOGGING.md).

---

## 15. Infrastructure Services

### Viewport Culling (`ViewportCuller`)

Only renders nodes and connections that are visible in the current viewport. For graphs with 500+ nodes, this provides dramatic performance improvements.

### Coordinate Conversion (`CoordinateConverter`)

Converts between screen coordinates (mouse position) and graph coordinates (node positions), accounting for pan offset and zoom level.

### Connection Validation (`ConnectionValidator`)

Validates that connections between sockets are type-compatible and follow the rules (output → input, same kind, no duplicates).

### Touch Gesture Handling (`TouchGestureHandler`)

Handles multi-touch gestures for pan, zoom, and interaction on mobile and tablet devices.

### Canvas Interaction (`CanvasInteractionHandler`)

Unified handler for all pointer, mouse, and keyboard events on the canvas. Coordinates dragging, selection, connection creation, and context menu display.

---

## 16. Built-in Standard Nodes

The `StandardNodeContext` provides ready-to-use nodes organized by category:

### Math
- **Add**, **Subtract**, **Multiply**, **Divide** — Basic arithmetic
- **Power**, **Sqrt**, **Abs** — Advanced math
- Number constants and random generators

### Strings
- **Concat**, **Split**, **Replace** — String manipulation
- **Length**, **Contains**, **Trim** — String inspection
- Format and template operations

### Lists
- **Map**, **Filter**, **Reduce** — Functional list operations
- **ForEach** — Iterate over list items
- List creation and manipulation

### Conditions
- **Branch (If)** — Conditional execution paths
- **Switch** — Multi-way branching
- **Compare** — Greater than, less than, equal comparisons

### Flow Control
- **Sequence** — Execute nodes in order
- **Parallel** — Execute nodes concurrently
- **Start** — Execution entry point

### Debug
- **Debug Print** — Output values to the terminal panel

---

## Service Lifetimes Summary

Understanding service lifetimes is important for correct usage:

| Service | Lifetime | Notes |
|---------|----------|-------|
| `NodeEditorState` | **Scoped** | One per Blazor circuit/user session |
| `NodeExecutionService` | **Scoped** | Tied to the user's state |
| `GraphSerializer` | **Scoped** | Uses registry (singleton) and state (scoped) |
| `HeadlessGraphRunner` | **Scoped** | Creates isolated execution contexts |
| `IPluginEventBus` | **Scoped** | Wired to the circuit's state |
| `NodeRegistryService` | **Singleton** | Shared node definitions across all sessions |
| `PluginLoader` | **Singleton** | One set of loaded plugins for the app |
| `SocketTypeResolver` | **Singleton** | Type mappings are global |
| `INodeEditorLogger` | **Singleton** | Centralized logging |
| `ILogChannelRegistry` | **Singleton** | Channel definitions are global |
| `PluginInstallationService` | **Scoped** | Per-session install operations |

---

## Platform Support

| Platform | Dynamic Plugin Loading | Full UI | Notes |
|----------|----------------------|---------|-------|
| Blazor Server | ✅ | ✅ | Recommended for full features |
| Windows Desktop | ✅ | ✅ | Via MAUI Blazor Hybrid |
| macOS Desktop | ✅ | ✅ | Via MAUI Blazor Hybrid |
| Linux Desktop | ✅ | ✅ | Via Blazor Server |
| Android | ✅ | ✅ | Via MAUI Blazor Hybrid |
| Blazor WebAssembly | ❌ | ✅ | No dynamic assembly loading |
| iOS | ❌ | ✅ | No dynamic assembly loading; use compile-time registration |

---

*For more details on any feature, see the linked reference documentation or the [API Reference](../NodeEditor.Blazor/docs/API.md).*
