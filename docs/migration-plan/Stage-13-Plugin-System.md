# Stage 13 — Plugin System (Desktop/Android Only)

## Status: 🟡 Partially Complete

### What's Done
- ✅ `PlatformGuard.IsPluginLoadingSupported()` in `Services/PlatformGuard.cs`
- ✅ iOS detection and short-circuit logic

### What's Remaining
- ❌ `INodePlugin` interface contract
- ❌ `PluginLoader` service for assembly scanning
- ❌ Plugin directory configuration
- ❌ Plugin manifest format
- ❌ Version compatibility checking
- ❌ Integration with `NodeRegistryService`

## Goal
Add an optional plugin discovery and registration pipeline for new node packs, while explicitly disabling plugin loading on iOS.

## Deliverables
- Plugin contracts: INodePlugin / INodeProvider
- NodeRegistryService plugin registration
- Platform-aware plugin loader

## Tasks
1. Define plugin contracts and version metadata.
2. Implement assembly scanning for node definitions.
3. Add PluginLoader with platform checks (disable on iOS).
4. Integrate plugin node packs into context menu categories.

## Acceptance Criteria
- Plugins can add nodes on Windows/Android/Mac Catalyst.
- iOS build does not attempt runtime plugin loading.

### Testing Parameters
- NUnit/xUnit test validates plugin discovery on desktop.
- NUnit/xUnit test asserts iOS path short-circuits plugin loading.

## Dependencies
Stages 06 and 10.

## Risks / Notes
- iOS AOT disallows runtime assembly loading.
- Only trusted plugins should be accepted.

## Architecture Notes
Plugins should be **opt-in** and **sandboxed** where possible. Provide a clear plugin lifecycle:
1. Discovery
2. Validation
3. Registration
4. Unload/disable (optional)

## Detailed Tasks (Expanded)
1. **Plugin contracts**
	- `INodePlugin` includes metadata and registration method.
2. **Plugin discovery**
	- Scan known directories or use config file.
3. **Validation**
	- Verify compatibility and API version.
4. **Registration**
	- Merge into `NodeRegistryService` categories.
5. **Platform guard**
	- Disable discovery on iOS with clear error logging.

## Code Examples

### Plugin contract
```csharp
public interface INodePlugin
{
	 string Name { get; }
	 Version ApiVersion { get; }
	 void Register(NodeRegistryService registry);
}
```

### Plugin loader guard
```csharp
if (!PlatformGuard.IsPluginLoadingSupported())
{
	 return; // no-op on iOS
}
```

## Missing Architecture Gaps (to close in this stage)
- **Plugin versioning strategy** and compatibility policy
- **Security posture** for untrusted plugins
- **Diagnostics** for failed plugin loading

## Implementation Notes (for next developer)

### Current Platform Guard
Located at `NodeEditor.Blazor/Services/PlatformGuard.cs`:
```csharp
public static bool IsPluginLoadingSupported()
{
    // iOS uses AOT compilation and doesn't support dynamic assembly loading
    return !OperatingSystem.IsIOS();
}
```

### Plugin Discovery Strategy
1. Define plugin directories (configurable)
2. Scan for DLLs in directories
3. Load assemblies via `AssemblyLoadContext`
4. Find types implementing `INodePlugin`
5. Validate API version compatibility
6. Call `plugin.Register(registry)`

### Recommended Files to Create
```
NodeEditor.Blazor/Services/Plugins/
├── INodePlugin.cs              # Plugin contract interface
├── PluginManifest.cs           # Metadata for plugins
├── PluginLoader.cs             # Assembly scanning and loading
├── PluginLoadContext.cs        # Isolated AssemblyLoadContext
└── PluginValidationResult.cs   # Validation outcome
```

### Plugin Contract
```csharp
public interface INodePlugin
{
    /// <summary>Plugin display name</summary>
    string Name { get; }
    
    /// <summary>Unique plugin identifier</summary>
    string Id { get; }
    
    /// <summary>Plugin version</summary>
    Version Version { get; }
    
    /// <summary>Minimum host API version required</summary>
    Version MinApiVersion { get; }
    
    /// <summary>Register nodes with the registry</summary>
    void Register(NodeRegistryService registry);
    
    /// <summary>Optional cleanup when plugin is unloaded</summary>
    void Unload() { }
}
```

### Plugin Manifest (Optional JSON)
```json
{
    "id": "com.example.mathnodes",
    "name": "Advanced Math Nodes",
    "version": "1.2.0",
    "minApiVersion": "1.0.0",
    "entryAssembly": "MathNodes.dll",
    "category": "Math"
}
```

### Plugin Loading Flow
```csharp
public class PluginLoader
{
    public async Task<IReadOnlyList<INodePlugin>> LoadPluginsAsync(
        string pluginDirectory,
        CancellationToken token = default)
    {
        if (!PlatformGuard.IsPluginLoadingSupported())
        {
            _logger.LogInformation("Plugin loading skipped (iOS)");
            return Array.Empty<INodePlugin>();
        }

        var plugins = new List<INodePlugin>();
        foreach (var dll in Directory.GetFiles(pluginDirectory, "*.dll"))
        {
            token.ThrowIfCancellationRequested();
            try
            {
                var plugin = LoadPlugin(dll);
                if (ValidatePlugin(plugin))
                {
                    plugins.Add(plugin);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load plugin: {Path}", dll);
            }
        }
        return plugins;
    }
}
```

### Security Considerations
- Plugins run with full trust (no sandboxing in .NET)
- Consider code signing for production
- Log all plugin loads for audit
- Allow disabling specific plugins via configuration

## Checklist
- [x] Platform guard exists for iOS
- [ ] Plugins load and register nodes on supported platforms
- [ ] iOS build explicitly disables plugin loading (logged)
- [ ] Plugin errors are surfaced in logs
- [ ] Version compatibility checked
- [ ] Plugin manifest validation
- [ ] Integration with NodeRegistryService
