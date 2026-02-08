![image](https://github.com/Johnny2x2/NodeEditor.Net/blob/main/Assets/Full-Logo.ico)
# NodeEditor.Net

[![CI](https://github.com/Johnny2x2/NodeEditor.Net/actions/workflows/ci.yml/badge.svg)](https://github.com/Johnny2x2/NodeEditor.Net/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

A powerful, event-driven **visual node editor** for .NET 10, enabling interactive graph-based visual programming with a full execution engine, plugin marketplace, AI integration (MCP), and headless execution support.

> **What is a node editor?** A node editor lets you visually build data-processing or logic graphs by connecting "nodes" together. Each node performs a small operation—add two numbers, format a string, branch on a condition—and the editor handles wiring, execution order, and data flow automatically. Think Unreal Blueprints, Blender Shader Nodes, or Node-RED.

## Features

- 🖱️ **Interactive Canvas** — Pan, zoom, drag nodes, box-select, and create connections with mouse or touch
- ⚡ **Execution Engine** — Run node graphs with parallel, sequential, or event-driven execution modes
- 🔌 **Plugin System** — Dynamically load custom node types from external assemblies with full lifecycle management
- 🏪 **Plugin Marketplace** — Browse, install, and manage plugins from local or remote repositories
- 💾 **Serialization** — Save and load node graphs as JSON with version migration support
- 🎨 **Custom Editors** — 9 built-in socket value editors (text, number, bool, dropdown, slider, image, etc.) plus extensibility for custom editors
- 🔒 **Type System** — Type-safe socket connections with automatic type resolution and validation
- 📊 **Graph Variables** — Define graph-scoped variables with auto-generated getter/setter nodes
- 📡 **Graph Events** — Event-driven execution with custom event nodes (trigger + listener)
- 🗂️ **Overlays** — Visual organizer shapes to group and annotate sections of your graph
- 🤖 **MCP Integration** — Model Context Protocol server for AI-assisted graph editing (Claude, Cursor, etc.)
- 🖥️ **Headless Execution** — Run node graphs programmatically without any UI (console apps, APIs, pipelines)
- 📝 **Structured Logging** — Channel-based logging with configurable retention policies
- 📱 **Touch Support** — Multi-touch gestures for pan, zoom, and interaction on mobile/tablet
- 🔍 **Viewport Culling** — Automatic performance optimization for large graphs (500+ nodes)
- 🧪 **Well Tested** — Comprehensive unit test coverage with bUnit + xUnit

## Quick Start

**1. Register services** in your `Program.cs`:

```csharp
using NodeEditor.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddNodeEditor();

var app = builder.Build();
```

**2. Add the canvas** to your Razor page:

```razor
@page "/editor"
@using NodeEditor.Blazor.Components
@using NodeEditor.Net.Services
@inject NodeEditorState EditorState

<NodeEditorCanvas State="@EditorState" />
```

**3. Define custom nodes** with attributes:

```csharp
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Execution;

public sealed class MathNodes : INodeContext
{
    [Node("Add", category: "Math", description: "Adds two numbers", isCallable: false)]
    public void Add(double A, double B, out double Result)
    {
        Result = A + B;
    }

    [Node("Start", category: "Flow", isCallable: true, isExecutionInitiator: true)]
    public void Start(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
```

For a complete walkthrough, see the [Getting Started Guide](docs/GETTING_STARTED.md).

## Project Structure

```
NodeEditor.Net              (pure .NET 10 class library — no Blazor SDK)
  ↑
NodeEditor.Blazor           (Razor component library — reusable Blazor UI)
  ↑
NodeEditor.Blazor.WebHost   (Blazor Server host app — the runnable demo)
```

| Project | Description |
|---------|-------------|
| **NodeEditor.Net** | Pure .NET 10 core library — execution engine, models, ViewModels, registry, plugins, serialization, logging, and headless execution. Use this to run node graphs in any .NET app (console, API, WPF, MAUI) without Blazor. |
| **NodeEditor.Blazor** | Razor component library — Blazor UI components (canvas, nodes, editors, marketplace), Blazor-specific services, and DI setup via `AddNodeEditor()`. |
| **NodeEditor.Blazor.WebHost** | Blazor Server host application for running the editor with MCP integration enabled. |
| **NodeEditor.Blazor.Tests** | Unit and integration tests (bUnit + xUnit) — 30+ test files covering state, execution, plugins, serialization, components, and MCP. |
| **NodeEditor.Mcp** | Model Context Protocol server for AI-assisted graph editing — supports both stdio and HTTP/SSE transports. |
| **NodeEditor.Plugins.Template** | Starter template for creating new plugins — copy and customize. |
| **NodeEditor.Plugins.TestA/TestB** | Example/test plugins demonstrating the plugin system. |

## Building

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Build the solution
dotnet build NodeEditor.slnx

# Run the web host
dotnet run --project NodeEditor.Blazor.WebHost/NodeEditor.Blazor.WebHost.csproj

# Run tests
dotnet test NodeEditor.Blazor.Tests/NodeEditor.Blazor.Tests.csproj

# Run the MCP server (standalone, stdio mode)
dotnet run --project NodeEditor.Mcp/NodeEditor.Mcp.csproj
```

## Architecture

The library uses a **3-tier, event-based MVVM** architecture:

- **NodeEditor.Net** (core) — Models (`NodeData`, `SocketData`, `ConnectionData`, `GraphData`), ViewModels (`NodeViewModel`, `SocketViewModel`), services (`NodeEditorState`, `NodeExecutionService`, `GraphSerializer`), plugin system with marketplace, logging infrastructure, and headless execution. No Blazor dependency — usable in console apps, APIs, or any .NET host.
- **NodeEditor.Blazor** (UI) — Razor components (`NodeEditorCanvas`, `NodeComponent`, `ConnectionPath`, `ContextMenu`, marketplace UI), built-in socket editors, and the `AddNodeEditor()` DI extension method.
- **NodeEditor.Blazor.WebHost** (host) — Blazor Server application that wires everything together, including MCP HTTP/SSE endpoint for AI integration.

### Key Patterns

- **Event-Driven State**: Components subscribe to `NodeEditorState` events (`NodeAdded`, `ConnectionRemoved`, `SelectionChanged`, etc.) for efficient, selective re-rendering.
- **Node Discovery**: Nodes are discovered via `[Node]` attributes on `INodeContext` methods and registered through `NodeRegistryService`.
- **Plugin Lifecycle**: Plugins implement `INodePlugin` with lifecycle hooks (`OnLoadAsync` → `Register` → `ConfigureServices` → `OnInitializeAsync` → `OnUnloadAsync`).
- **State Bridge**: The `INodeEditorStateBridge` singleton connects the scoped Blazor circuit state to singleton services like MCP, enabling real-time AI-controlled editing.

## Key Capabilities

### Execution Engine

The execution engine transforms visual node graphs into runnable computations with support for:
- **Sequential execution** — Follow execution paths through callable nodes
- **Parallel execution** — Run independent data nodes concurrently
- **Event-driven execution** — Trigger execution via custom events
- **Headless execution** — Run graphs without any UI via `HeadlessGraphRunner`

### Graph Variables & Events

- **Variables**: Define named, graph-scoped variables that auto-generate Get/Set nodes. Variables are seeded before execution and stay synchronized across all referencing nodes.
- **Events**: Define custom events with auto-generated Trigger and Listener nodes for event-driven flow control within your graph.

### Plugin Marketplace

Browse, install, update, and uninstall plugins from local or remote repositories. The marketplace supports:
- Local directory-based repositories
- Remote HTTP repositories with token-based authentication
- Cached metadata for offline browsing
- Plugin manifest validation and version compatibility checking

### MCP Integration

The Model Context Protocol server enables AI assistants (Claude, Cursor, etc.) to programmatically build and execute node graphs. It supports:
- **Stdio transport** — Run headless as a standalone process
- **HTTP/SSE transport** — Embed in the WebHost for real-time canvas control
- **60+ abilities** organized across 8 categories (catalog, nodes, connections, graph, execution, plugins, logging, overlays)

### Built-in Standard Nodes

The `StandardNodeContext` provides built-in nodes for:
- **Math**: Add, Subtract, Multiply, Divide, Power, and more
- **Strings**: Concat, Split, Replace, Length, Contains
- **Lists**: Map, Filter, Reduce, ForEach
- **Conditions**: If/Branch, Switch, Comparison operators
- **Flow**: Parallel execution, sequence control
- **Debug**: Print to output terminal

## Plugins

Plugins extend the editor with custom node types. See the [Plugin SDK](docs/reference/PLUGIN_SDK.md) and [Plugin Customization Guide](docs/reference/PLUGIN_CUSTOMIZATION.md) for details.

```bash
# Publish a plugin to the local repository
./publish-plugin.ps1 -PluginProject path/to/YourPlugin.csproj

# Publish all plugins
./publish-all-plugins.ps1
```

## Documentation

### Guides

| Document | Description |
|----------|-------------|
| [Getting Started](docs/GETTING_STARTED.md) | Step-by-step setup and first graph tutorial |
| [Features Overview](docs/FEATURES.md) | Comprehensive guide to all features and how to use them |
| [FAQ](docs/FAQ.md) | Frequently asked questions |

### Reference

| Document | Description |
|----------|-------------|
| [Execution Engine](docs/reference/EXECUTION_ENGINE.md) | Deep dive into graph execution, planning, and runtime |
| [Event-Based Architecture](docs/reference/EventBasedArchitecture.md) | State management patterns and Blazor integration |
| [Plugin SDK](docs/reference/PLUGIN_SDK.md) | How to create plugins from the template project |
| [Plugin Customization](docs/reference/PLUGIN_CUSTOMIZATION.md) | Full reference for plugin capabilities |
| [Plugin Marketplace](docs/reference/MARKETPLACE.md) | Browsing, installing, and publishing plugins |
| [Graph Variables & Events](docs/reference/VARIABLES_AND_EVENTS.md) | Graph-scoped variables and custom event nodes |
| [Overlays](docs/reference/OVERLAYS.md) | Organizer shapes for annotating your graph |
| [Headless Execution](docs/reference/HEADLESS_EXECUTION.md) | Running node graphs without a UI |
| [Logging](docs/reference/LOGGING.md) | Channel-based structured logging system |
| [MCP State Bridge](docs/reference/MCP_STATE_BRIDGE.md) | How MCP connects to the live Blazor editor |
| [Architecture Review](docs/reference/ARCHITECTURE_REVIEW.md) | Code review, design patterns, and improvement roadmap |
| [MCP Integration](NodeEditor.Mcp/README.md) | Model Context Protocol server documentation |

### Blazor Component Library

| Document | Description |
|----------|-------------|
| [Blazor API Reference](NodeEditor.Blazor/docs/API.md) | Complete API reference for all components and services |
| [Custom Nodes Tutorial](NodeEditor.Blazor/docs/CUSTOM-NODES.md) | Step-by-step guide to creating custom nodes and editors |
| [Wiring & Event Flow](NodeEditor.Blazor/docs/WiringAndEventFlow.md) | How components communicate through state events |
| [Migration Guide](NodeEditor.Blazor/docs/MIGRATION.md) | Migrating from WinForms NodeEditor |
| [Troubleshooting](NodeEditor.Blazor/docs/TROUBLESHOOTING.md) | Common issues and solutions |
| [Plugin Extensibility Roadmap](NodeEditor.Blazor/docs/PluginExtensibilityRoadmap.md) | Planned plugin system enhancements |

## License

[MIT](LICENSE.txt)
