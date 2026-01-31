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
