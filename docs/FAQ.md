# Frequently Asked Questions

## General

### What is NodeEditor.Net?

NodeEditor.Net is a visual node editor for .NET 10. It lets you build data-processing and logic graphs by connecting "nodes" together in a browser-based canvas. Each node performs a small operation (add two numbers, format a string, branch on a condition) and the editor handles wiring, execution order, and data flow automatically.

### Who is this for?

- **Application developers** who want to add visual programming to their apps
- **Tool builders** creating no-code/low-code automation interfaces
- **Game developers** implementing visual scripting (similar to Unreal Blueprints)
- **Data engineers** building visual data pipelines
- **AI developers** using MCP to let AI assistants build and execute graphs

### What platforms does it run on?

NodeEditor.Net is built on .NET 10 and Blazor. It runs on:
- **Blazor Server** (recommended, full features including dynamic plugins)
- **MAUI Blazor Hybrid** (Windows, macOS, Android)
- **Blazor WebAssembly** (no dynamic plugin loading)
- **Console/API** (headless execution only, no UI)

### Is this open source?

Yes, NodeEditor.Net is released under the [MIT License](../LICENSE.txt).

---

## Setup

### What .NET version do I need?

.NET 10 SDK. Download it from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0).

### How do I build the project?

```bash
dotnet build NodeEditor.slnx
```

### How do I run the demo app?

```bash
dotnet run --project NodeEditor.Blazor.WebHost/NodeEditor.Blazor.WebHost.csproj
```

Then open `https://localhost:5001` (or the port shown in the console) in your browser.

### How do I run the tests?

```bash
dotnet test NodeEditor.Blazor.Tests/NodeEditor.Blazor.Tests.csproj
```

---

## Architecture

### Why are there three projects (NodeEditor.Net, NodeEditor.Blazor, NodeEditor.Blazor.WebHost)?

This 3-tier architecture separates concerns:

1. **NodeEditor.Net** — Pure .NET core library with no Blazor dependency. Contains models, execution engine, plugins, serialization, and logging. You can use this in console apps, APIs, or any .NET host.
2. **NodeEditor.Blazor** — Razor component library that provides the UI. References NodeEditor.Net and adds Blazor-specific components and services.
3. **NodeEditor.Blazor.WebHost** — A runnable Blazor Server app that wires everything together as a demo.

### Can I use NodeEditor.Net without Blazor?

Yes! The `NodeEditor.Net` library has no Blazor dependency. You can use `HeadlessGraphRunner` to execute graphs in console apps, APIs, or background services. See the [Headless Execution](reference/HEADLESS_EXECUTION.md) guide.

### What is the `AddNodeEditor()` call?

It's a DI extension method in `NodeEditor.Blazor.Services` that registers all required services (state, execution, registry, plugins, serialization, logging, etc.) with their correct lifetimes. Call it once in `Program.cs`.

---

## Nodes

### How do I create a custom node?

1. Create a class that implements `INodeContext`
2. Add methods with the `[Node]` attribute
3. Register the assembly with `NodeRegistryService`

See the [Getting Started Guide](GETTING_STARTED.md) and [Custom Nodes Tutorial](../NodeEditor.Blazor/docs/CUSTOM-NODES.md).

### What's the difference between callable and non-callable nodes?

- **Non-callable** (`isCallable: false`): Data nodes that compute outputs from inputs. They have no execution flow and evaluate lazily.
- **Callable** (`isCallable: true`): Nodes with execution flow sockets (`ExecutionPath`). They run when their execution input is triggered.

### What is an execution initiator?

An execution initiator (`isExecutionInitiator: true`) is a callable node that starts an execution chain. It has no execution input—it's the entry point. Think of it as the "Start" button for a section of your graph.

### Can I use async methods in nodes?

Yes, node methods can return `Task` for async operations.

---

## Plugins

### How do I create a plugin?

1. Copy the `NodeEditor.Plugins.Template` project
2. Implement `INodePlugin` and your `INodeContext` classes
3. Add a `plugin.json` manifest
4. Build and publish to the plugin repository

See the [Plugin SDK](reference/PLUGIN_SDK.md).

### Why don't plugins load on iOS or WebAssembly?

These platforms don't support dynamic assembly loading (`AssemblyLoadContext`). On iOS, register plugins at compile time instead:

```csharp
var registry = serviceProvider.GetRequiredService<NodeRegistryService>();
registry.RegisterFromAssembly(typeof(MyPlugin).Assembly);
```

### How do I install a plugin from the marketplace?

Open the Plugin Manager (button in the editor toolbar), browse or search for plugins, and click "Install."

---

## Execution

### How do I execute a graph?

Click the "Execute" button in the editor, or programmatically:

```csharp
var service = serviceProvider.GetRequiredService<NodeExecutionService>();
await service.ExecuteAsync(nodes, connections, context, nodeContext, options, token);
```

### What execution modes are available?

- **Sequential** — Follow execution paths through callable nodes
- **Parallel** — Run independent data nodes concurrently

### Can I run graphs without a UI?

Yes, use `HeadlessGraphRunner`. See [Headless Execution](reference/HEADLESS_EXECUTION.md).

---

## MCP

### What is MCP?

The [Model Context Protocol](https://modelcontextprotocol.io/) is a standard for connecting AI assistants to external tools. NodeEditor.Net's MCP server lets AI assistants like Claude and Cursor build and execute node graphs programmatically.

### How do I enable MCP?

Add to `appsettings.json`:
```json
{ "Mcp": { "Enabled": true, "RoutePattern": "/mcp" } }
```

Then generate an API key from the Settings panel in the editor.

### Can AI build graphs in real-time?

Yes! In HTTP/SSE mode, the MCP server uses the State Bridge pattern to control the live canvas. When an AI adds a node via MCP, it appears on the user's screen immediately.

---

## Troubleshooting

### Nodes don't appear in the context menu

Make sure you've registered the assembly:
```csharp
Registry.EnsureInitialized(new[] { typeof(MyNodes).Assembly });
```

### UI doesn't update after state changes

Ensure you call `StateHasChanged()` in event handlers and always use `EditorState.AddNode()` instead of directly modifying collections.

### Plugin loading fails

Check that:
1. The plugin directory exists and contains the DLL
2. `plugin.json` is present and valid
3. The plugin's `MinApiVersion` is compatible with the host
4. Check console/logs for detailed error messages

For more troubleshooting, see the [Troubleshooting Guide](../NodeEditor.Blazor/docs/TROUBLESHOOTING.md).
