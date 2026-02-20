# Copilot instructions for NodeEditor.Net

## Big picture
- This repo has a 3-tier architecture: `NodeEditor.Net` (pure .NET 10 core library — models, ViewModels, services, execution, plugins, serialization) → `NodeEditor.Blazor` (Razor component library — Blazor UI components and Blazor-specific services) → `NodeEditor.Blazor.WebHost` (runnable Blazor Server demo).
- State is centralized in `NodeEditor.Net/Services/Core/NodeEditorState.cs`; UI components subscribe to its events for rendering (`NodeEditor.Blazor/Components/*`).
- Models vs ViewModels are separated: data classes in `NodeEditor.Net/Models` and bindable wrappers in `NodeEditor.Net/ViewModels`.
- Nodes are defined by subclassing `NodeBase`, overriding `Configure(INodeBuilder)` for metadata/sockets and `ExecuteAsync(INodeExecutionContext, CancellationToken)` for logic. Discovered via `NodeDiscoveryService` and registered through `INodeRegistryService`.
- Plugins implement `INodePlugin` and are loaded via `PluginLoader`; manifests live in `plugin.json`.

## Key files to orient
- Core state management: `NodeEditor.Net/Services/Core/NodeEditorState.cs`
- DI setup and service registrations: `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs`
- Canvas + node rendering: `NodeEditor.Blazor/Components/NodeEditorCanvas.razor`, `NodeEditor.Blazor/Components/NodeComponent.razor`
- Connection rendering: `NodeEditor.Blazor/Components/ConnectionPath.razor`
- Styling: `NodeEditor.Blazor/wwwroot/css/node-editor.css`
- Web host entry: `NodeEditor.Blazor.WebHost/Program.cs`
- Reference docs: `docs/reference/` (architecture, execution engine, plugin SDK, events)

## Workflows
- Build solution: `dotnet build NodeEditor.slnx`
- Run web host: `dotnet watch run --project NodeEditor.Blazor.WebHost/NodeEditor.Blazor.WebHost.csproj`
- Run tests: `dotnet test NodeEditor.Blazor.Tests/NodeEditor.Blazor.Tests.csproj`
  - Tests use bUnit + Playwright and dynamically load plugins; build targets copy plugin outputs into `bin/.../plugins/*` for test discovery.

## Conventions and patterns
- Prefer event-based updates: components subscribe/unsubscribe to `NodeEditorState` events instead of polling state.
- Keep UI logic in Razor components and data/behavior in ViewModels/Services.
- Socket types are resolved via `SocketTypeResolver`; connection rules live in services, not UI.
- Plugin repository format is documented in `plugin-repository/README.md`; publish with `publish-plugin.ps1` or `publish-all-plugins.ps1`.

## Integration points
- Plugins live under `NodeEditor.Plugins.*` and are loaded dynamically; include `plugin.json` alongside plugin DLLs.
- Tests reference plugin projects with `ReferenceOutputAssembly=false` and load them from the copied `plugins` folder.
