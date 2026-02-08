# Plugin Repository

This folder serves as the **local plugin marketplace repository**.

## For Users

Plugins published here will appear in the **Plugin Manager** UI within the app.
Open the app, click "Plugins", and install any plugins you want.

## For Plugin Developers

See the SDK guide: [docs/reference/PLUGIN_SDK.md](../docs/reference/PLUGIN_SDK.md)

To publish your plugin to this repository:

```powershell
./publish-plugin.ps1 -PluginProject path/to/YourPlugin.csproj
```

Or publish all plugins in the solution:

```powershell
./publish-all-plugins.ps1
```

The publish script also generates default marketplace metadata (if missing) and creates a ZIP package under:

```
plugin-packages/<ProjectName>/<ProjectName>-<version>.zip
```

## Plugin Structure

Each plugin folder should contain:

```
PluginName/
├── plugin.json                 # Required: Plugin manifest
├── plugin-marketplace.json     # Optional: Extended marketplace metadata
├── PluginName.dll              # Required: Main plugin assembly
├── PluginName.deps.json        # Optional: Dependency manifest
└── [dependencies].dll          # Any additional DLLs
```

## plugin.json Format

```json
{
  "id": "com.yourcompany.pluginname",
  "name": "Your Plugin Name",
  "version": "1.0.0",
  "minApiVersion": "1.0.0",
  "entryAssembly": "YourPlugin.dll",
  "category": "Category"
}
```
