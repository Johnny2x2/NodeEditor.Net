# Copilot instructions for NodeEditorMax

## Big picture
- This repo centers on the NodeEditor.Blazor component library (event-driven state + MVVM) with a web host and plugins.
- State is centralized in `NodeEditor.Blazor/Services/NodeEditorState.cs`; UI components subscribe to its events for rendering (`NodeEditor.Blazor/Components/*`).
- Models vs ViewModels are separated: data classes in `NodeEditor.Blazor/Models` and bindable wrappers in `NodeEditor.Blazor/ViewModels`.
- Nodes are discovered via `[Node]` attributes on `INodeContext` methods and registered through `NodeRegistryService`.
- Plugins implement `INodePlugin` and are loaded via `PluginLoader`; manifests live in `plugin.json`.

## Key files to orient
- DI setup and service registrations: `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs`
- Canvas + node rendering: `NodeEditor.Blazor/Components/NodeEditorCanvas.razor`, `NodeEditor.Blazor/Components/NodeComponent.razor`
- Connection rendering: `NodeEditor.Blazor/Components/ConnectionPath.razor`
- Styling: `NodeEditor.Blazor/wwwroot/css/node-editor.css`
- Web host entry: `NodeEditor.Blazor.WebHost/Program.cs`
- Migration architecture notes: `docs/migration-plan/README.md`

## Workflows
- Build solution: `dotnet build NodeEditorMax/NodeEditorMax.csproj`
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
