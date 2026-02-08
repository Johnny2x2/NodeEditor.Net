# Plugin Marketplace

The marketplace system lets users browse, search, install, update, and uninstall plugins from both local and remote repositories through a built-in UI.

## Overview

The plugin marketplace provides a complete plugin management experience:
- **Browse** available plugins from local or remote repositories
- **Search** by name, category, or tags
- **Install** plugins with one click
- **Uninstall** plugins cleanly
- **View details** including description, version, author, and release notes
- **Configure** marketplace sources

## Architecture

```
PluginManagerDialog (UI)
├── PluginSearchBar — filter plugins
├── PluginCard — display card per plugin
├── PluginDetailsPanel — detailed view
└── MarketplaceSettingsPanel — source config

IPluginInstallationService (scoped)
├── Install / Uninstall operations
├── Version compatibility checks
└── File system operations

AggregatedPluginMarketplaceSource
├── LocalPluginMarketplaceSource — scans local directories
└── RemotePluginMarketplaceSource — HTTP-based remote repos

IPluginMarketplaceCache (singleton)
└── FileBasedMarketplaceCache — on-disk metadata cache

IPluginMarketplaceAuthProvider
└── TokenBasedAuthProvider — API token for private repos
```

## Marketplace Sources

### Local Source

The `LocalPluginMarketplaceSource` scans a directory for `plugin.json` and optional `plugin-marketplace.json` files:

```
plugin-repository/
├── MyPlugin/
│   ├── plugin.json                 # Required: Plugin manifest
│   ├── plugin-marketplace.json     # Optional: Extended metadata
│   ├── MyPlugin.dll                # Plugin assembly
│   └── MyPlugin.deps.json          # Dependency manifest
└── AnotherPlugin/
    ├── plugin.json
    └── AnotherPlugin.dll
```

### Remote Source

The `RemotePluginMarketplaceSource` fetches plugin metadata over HTTP from a remote repository. It supports token-based authentication for private repositories.

### Aggregated Source

The `AggregatedPluginMarketplaceSource` combines multiple sources (local + remote) into a single unified catalog.

## Plugin Manifest Files

### `plugin.json` (Required)

Minimum metadata required by the plugin loader:

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

### `plugin-marketplace.json` (Optional)

Extended metadata for marketplace display:

```json
{
  "Id": "com.example.myplugin",
  "DisplayName": "My Awesome Plugin",
  "Description": "A collection of useful utility nodes for data processing",
  "Author": "Your Name",
  "AuthorUrl": "https://github.com/yourname",
  "Tags": ["utilities", "data", "processing"],
  "IconUrl": "https://example.com/icon.png",
  "ReleaseNotes": "v1.0.0: Initial release with 5 nodes",
  "License": "MIT"
}
```

## Installation Service

The `IPluginInstallationService` handles the mechanics of installing and uninstalling plugins:

```csharp
// Install a plugin from the marketplace
var result = await installationService.InstallAsync(pluginInfo, cancellationToken);

if (result.Success)
{
    Console.WriteLine($"Installed {result.PluginName} v{result.Version}");
}
else
{
    Console.WriteLine($"Failed: {result.ErrorMessage}");
}

// Uninstall a plugin
var uninstallResult = await installationService.UninstallAsync(pluginId, cancellationToken);
```

### Installation Flow

1. **Download**: Plugin files are downloaded from the source
2. **Validate**: Plugin manifest and API version are checked
3. **Extract**: Files are placed in the plugins directory
4. **Load**: The `PluginLoader` loads the new assembly
5. **Register**: Plugin nodes are registered with the registry
6. **Notify**: UI is updated to reflect the new plugin

## Publishing Plugins

### Using Publish Scripts

```powershell
# Publish a single plugin to the local repository
./publish-plugin.ps1 -PluginProject path/to/YourPlugin.csproj

# Publish all plugin projects in the solution
./publish-all-plugins.ps1
```

The publish script:
1. Builds the plugin project
2. Copies DLLs and manifests to `plugin-repository/{PluginName}/`
3. Generates a default `plugin-marketplace.json` if missing
4. Creates a ZIP archive in `plugin-packages/{PluginName}/`

### Manual Publishing

1. Build your plugin project
2. Copy the output to a folder in the `plugin-repository/` directory
3. Ensure `plugin.json` is present alongside the DLL
4. Optionally add `plugin-marketplace.json` for marketplace display

## Marketplace Cache

The `FileBasedMarketplaceCache` caches remote marketplace metadata on disk for offline browsing and faster startup. Cache is refreshed when the user opens the Plugin Manager.

## Authentication

For private remote repositories, configure a `TokenBasedAuthProvider`:

```csharp
services.Configure<MarketplaceOptions>(options =>
{
    options.RemoteRepositoryUrl = "https://plugins.example.com/api";
    options.AuthToken = "your-api-token";
});
```

## Configuration

### `MarketplaceOptions`

```csharp
public class MarketplaceOptions
{
    public string LocalRepositoryPath { get; set; } = "plugin-repository";
    public string? RemoteRepositoryUrl { get; set; }
    public string? AuthToken { get; set; }
    public string InstalledPluginsPath { get; set; } = "plugins";
}
```

## UI Components

| Component | Description |
|-----------|-------------|
| `PluginManagerDialog` | Full-screen dialog for browsing and managing plugins |
| `PluginCard` | Card view showing plugin name, author, version, and install button |
| `PluginDetailsPanel` | Detailed view with description, tags, release notes, and action buttons |
| `PluginSearchBar` | Search and filter controls |
| `MarketplaceSettingsPanel` | Configure local and remote marketplace sources |

## MCP Integration

Plugin management is accessible via MCP abilities:

| Ability | Description |
|---------|-------------|
| `plugin.list_loaded` | List all currently loaded plugins |
| `plugin.list_installed` | List all installed plugins |
| `plugin.install` | Install a plugin from the marketplace |
| `plugin.uninstall` | Uninstall a plugin |
| `plugin.enable` | Enable a disabled plugin |
| `plugin.disable` | Disable a plugin without uninstalling |
| `plugin.reload` | Reload a plugin (unload + load) |

## Namespaces

| Type | Namespace |
|------|-----------|
| `IPluginInstallationService` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `PluginInstallationService` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `IPluginMarketplaceSource` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `LocalPluginMarketplaceSource` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `RemotePluginMarketplaceSource` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `AggregatedPluginMarketplaceSource` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `IPluginMarketplaceCache` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `IPluginMarketplaceAuthProvider` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `MarketplaceOptions` | `NodeEditor.Net.Services.Plugins.Marketplace` |
| `MarketplacePluginInfo` | `NodeEditor.Net.Services.Plugins.Marketplace.Models` |
| `InstalledPluginInfo` | `NodeEditor.Net.Services.Plugins.Marketplace.Models` |
