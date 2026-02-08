# NodeEditor.Blazor

[![CI](https://github.com/Johnny2x2/NodeEditorMax/actions/workflows/ci.yml/badge.svg)](https://github.com/Johnny2x2/NodeEditorMax/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE.txt)

A powerful, event-driven **node editor component library** for Blazor, supporting interactive graph-based visual programming with node execution, plugin extensibility, and serialization.

## Features

- 🖱️ **Interactive Canvas** — Pan, zoom, drag nodes, and create connections
- ⚡ **Execution Engine** — Run node graphs with parallel or sequential execution
- 🔌 **Plugin System** — Dynamically load custom node types from external assemblies
- 💾 **Serialization** — Save and load node graphs with version migration support
- 🎨 **Custom Editors** — Built-in socket value editors with extensibility
- 🔒 **Type System** — Type-safe socket connections with automatic resolution
- 🧪 **Well Tested** — Comprehensive unit test coverage with bUnit
- 🤖 **MCP Integration** — Model Context Protocol server for AI-assisted graph editing

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
@using NodeEditor.Blazor.Services
@inject NodeEditorState EditorState

<NodeEditorCanvas State="@EditorState" />
```

**3. Define custom nodes** with attributes:

```csharp
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

## Project Structure

| Project | Description |
|---------|-------------|
| **NodeEditor.Blazor** | Core component library — models, ViewModels, services, and Razor components |
| **NodeEditor.Blazor.WebHost** | Blazor Server host application for running the editor |
| **NodeEditor.Blazor.Tests** | Unit and integration tests (bUnit + xUnit) |
| **NodeEditor.Mcp** | Model Context Protocol server for AI-assisted graph editing |
| **NodeEditor.Plugins.*** | Example plugins demonstrating the plugin system |

## Building

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```bash
# Build the solution
dotnet build NodeEditorMax.slnx

# Run the web host
dotnet run --project NodeEditor.Blazor.WebHost/NodeEditor.Blazor.WebHost.csproj

# Run tests
dotnet test NodeEditor.Blazor.Tests/NodeEditor.Blazor.Tests.csproj
```

## Architecture

The library uses an **event-based MVVM** architecture optimized for Blazor's rendering model:

- **Models** (`NodeData`, `ConnectionData`, `SocketData`) — Immutable data records
- **ViewModels** (`NodeViewModel`, `SocketViewModel`) — Bindable wrappers with change notifications
- **Services** (`NodeEditorState`, `NodeExecutionService`, `GraphSerializer`) — Centralized state and logic
- **Components** (`NodeEditorCanvas`, `NodeComponent`, `ConnectionPath`) — Razor UI layer

Nodes are discovered via `[Node]` attributes on `INodeContext` methods and registered through `NodeRegistryService`. Plugins implement `INodePlugin` and are loaded dynamically at runtime.

## Plugins

Plugins extend the editor with custom node types. See the [Plugin SDK](docs/reference/PLUGIN_SDK.md) and [Plugin Customization Guide](docs/reference/PLUGIN_CUSTOMIZATION.md) for details.

```bash
# Publish plugins to local repository
./publish-plugin.ps1 -PluginProject path/to/YourPlugin.csproj
```

## Documentation

| Document | Description |
|----------|-------------|
| [Execution Engine](docs/reference/EXECUTION_ENGINE.md) | Deep dive into graph execution, planning, and runtime |
| [Event-Based Architecture](docs/reference/EventBasedArchitecture.md) | State management patterns and Blazor integration |
| [Plugin SDK](docs/reference/PLUGIN_SDK.md) | How to create plugins from the template project |
| [Plugin Customization](docs/reference/PLUGIN_CUSTOMIZATION.md) | Full reference for plugin capabilities |
| [Architecture Review](docs/reference/ARCHITECTURE_REVIEW.md) | Code review, design patterns, and improvement roadmap |
| [MCP Integration](NodeEditor.Mcp/README.md) | Model Context Protocol server documentation |

## License

[MIT](LICENSE.txt)
