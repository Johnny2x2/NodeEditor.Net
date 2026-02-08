# Plugin SDK (Template Project)

This repository includes a **Plugin SDK template project** to make new plugins copy‑paste simple while staying consistent with the runtime architecture.

## What it is

- A minimal plugin project in the solution: [NodeEditor.Plugins.Template/NodeEditor.Plugins.Template.csproj](../../NodeEditor.Plugins.Template/NodeEditor.Plugins.Template.csproj)
- A single sample node in [NodeEditor.Plugins.Template/TemplatePlugin.cs](../../NodeEditor.Plugins.Template/TemplatePlugin.cs)
- Required manifests:
  - [NodeEditor.Plugins.Template/plugin.json](../../NodeEditor.Plugins.Template/plugin.json) (required)
  - [NodeEditor.Plugins.Template/plugin-marketplace.json](../../NodeEditor.Plugins.Template/plugin-marketplace.json) (optional but recommended)

## Why this exists

Plugins are discovered and loaded dynamically. The SDK template ensures every new plugin has the minimal required pieces so the loader and marketplace can recognize and install it without extra wiring.

Key mechanics:

- `plugin.json` is the **loader contract**. It defines `id`, `entryAssembly`, versioning, and the minimum supported API.
- `INodePlugin` (in `NodeEditor.Net.Services.Plugins`) is the **registration contract**. The host calls `Register()` to allow the plugin to register its nodes, and invokes lifecycle hooks (`OnLoadAsync`, `ConfigureServices`, `OnInitializeAsync`, `OnUnloadAsync`).
- `INodeContext` + `[Node]` attributes are the **node discovery contract**. `NodeRegistryService.RegisterFromAssembly(...)` scans the assembly for these nodes.

## Quick start (copy/paste flow)

1. Copy [NodeEditor.Plugins.Template](../../NodeEditor.Plugins.Template) to a new folder.
2. Rename the project file and update the namespace in `TemplatePlugin.cs`.
3. Update [plugin.json](../../NodeEditor.Plugins.Template/plugin.json):
   - `id` → unique reverse‑DNS id
   - `name` → display name
   - `entryAssembly` → your new DLL name
   - `category` → marketplace category
4. Update the sample node or add new `[Node]` methods in your `INodeContext` class.
5. Optional: update `plugin-marketplace.json` with descriptions, tags, URLs, and release notes.

## Build + package for marketplace

Use the publish script to build, publish, and package your plugin:

```powershell
./publish-plugin.ps1 -PluginProject path/to/YourPlugin.csproj
```

What it does:

- Builds the project
- Copies the DLLs and manifests into the local repository: [plugin-repository](../../plugin-repository)
- Generates a **default** `plugin-marketplace.json` if missing
- Creates a ZIP in `plugin-packages/<ProjectName>/<ProjectName>-<version>.zip`

This ZIP can be used for distribution or marketplace ingestion.

## Notes for future AI

- The SDK template is intentionally minimal: one `INodePlugin`, one `INodeContext`, one sample node.
- The host relies on `plugin.json` + `entryAssembly` to load the plugin assembly.
- The node registry uses reflection on `[Node]` attributes. Minimal nodes should be simple methods with input parameters and `out` return parameters.
- Marketplace metadata is optional but recommended. If missing, publish‑script generation creates a placeholder file that should be filled in before release.
