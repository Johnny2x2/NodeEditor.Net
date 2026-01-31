# Stage 13 — Plugin System (Desktop/Android Only)

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

## Checklist
- [ ] Plugins load and register nodes on supported platforms
- [ ] iOS build explicitly disables plugin loading
- [ ] Plugin errors are surfaced in logs
