# Stage 12 — Documentation & Migration Guide

## Status: � Complete

### What's Done
- ✅ Stage documentation exists in `docs/migration-plan/`
- ✅ Inline XML docs on services and models
- ✅ Library README with quick start (NodeEditor.Blazor/README.md)
- ✅ API reference documentation (NodeEditor.Blazor/docs/API.md)
- ✅ Migration guide (WinForms → Blazor mapping) (NodeEditor.Blazor/docs/MIGRATION.md)
- ✅ Working sample project (samples/BasicNodeEditor/)
- ✅ Custom node tutorial (NodeEditor.Blazor/docs/CUSTOM-NODES.md)
- ✅ Troubleshooting guide (NodeEditor.Blazor/docs/TROUBLESHOOTING.md)

### What's Remaining
- None - Stage 12 is complete!

### What Should Be Done Next
1. Write top-level README with install + minimal usage example.
2. Generate API reference for public types (services, models, components).
3. Create MIGRATION.md with WinForms → Blazor mapping and examples.
4. Add a working sample project and run instructions (MAUI + WebHost).
5. Write a custom node + custom editor tutorial using `INodeContext`.

## Goal
Provide clear guidance for users migrating from WinForms to MAUI Blazor.

## Requirements
- Provide a top-level README with installation, configuration, and minimal usage example.
- Publish API reference documentation for all public types and parameters.
- Supply a migration guide with a WinForms → Blazor mapping table and step-by-step migration flow.
- Include a working sample project with build/run instructions for MAUI and Blazor WebHost.
- Document custom node creation and custom editor UI using `INodeContext` and RenderFragments.
- Document `GraphSerializer` save/load usage and `GraphLibraryService` sample graph workflow.
- Provide troubleshooting guidance for common issues (rendering, input, plugins, serialization).
- Explicitly document plugin limitations on iOS and AOT constraints.
- Keep docs versioned and aligned with the package version.

## Deliverables
- Updated README for new library
- Migration guide with before/after examples

## Tasks
1. Document component API usage.
2. Provide examples for custom nodes and editors.
3. Outline breaking changes and replacements.
4. Document plugin support and iOS restriction.

## Acceptance Criteria
- Developers can embed the editor without additional support.
- Migration steps are unambiguous and complete.
- Plugin guidance clearly states iOS limitation.

### Testing Parameters
- NUnit/xUnit documentation walkthrough results in a running sample without manual fixes.
- NUnit/xUnit API reference checks match current public parameters and events.
- NUnit/xUnit doc validation ensures plugin guidance includes iOS restriction.

## Dependencies
Stage 10.

## Risks / Notes
- Keep API documentation consistent with actual component parameters.

## Architecture Notes
Documentation should include **API reference**, **migration guide**, **usage recipes**, and **troubleshooting**.

## Detailed Tasks (Expanded)
1. **API overview**
	- Public models, view models, and services list.
2. **How-to guides**
	- Create nodes, connect, execute, save, load.
3. **Custom editor tutorial**
	- Step-by-step example with RenderFragment usage.
4. **Migration map**
	- WinForms class → Blazor class mapping table.
5. **Known limitations**
	- iOS plugin restriction, AOT limitations.

## Code Examples

### Migration mapping (sample)
```
WinForms Node     -> NodeData + NodeViewModel
nSocket          -> SocketData + SocketViewModel
NodeManager      -> NodeExecutionService
NodeGraph        -> NodeEditorCanvas
```

## Missing Architecture Gaps (to close in this stage)
- **Versioned docs** aligned with package versions
- **Sample project** with working graph

## Implementation Notes (for next developer)

### Documentation Structure
```
docs/
├── README.md                    # Quick start, installation
├── API.md                       # Public API reference
├── MIGRATION.md                 # WinForms to Blazor guide
├── CUSTOM-NODES.md              # Creating custom nodes
├── TROUBLESHOOTING.md           # Common issues and solutions
└── samples/
    └── BasicNodeEditor/         # Complete working sample
```

### Migration Mapping Table
Include in `MIGRATION.md`:

| WinForms (Legacy) | Blazor (New) | Notes |
|-------------------|--------------|-------|
| `NodeGraph` | `NodeEditorCanvas` | Main canvas component |
| `NodeVisual` | `NodeViewModel` + `NodeComponent` | Separated model and view |
| `SocketVisual` | `SocketViewModel` + `SocketComponent` | |
| `NodeConnection` | `ConnectionData` | Immutable record |
| `NodeManager` | `NodeExecutionService` | Execution engine |
| `INodesContext` | `INodeContext` | Node method container |
| `NodeAttribute` | `NodeAttribute` (ported) | Same concept |
| `FeedbackInfo` | `FeedbackInfo` (ported) | Execution control |
| `NodeControl` | `NodeEditorState` | State management |

### Quick Start Guide
```markdown
## Installation

1. Add project reference to NodeEditor.Blazor
2. Register services in Program.cs:
   ```csharp
   builder.Services.AddNodeEditor();
   ```
3. Add CSS to index.html:
   ```html
   <link rel="stylesheet" href="_content/NodeEditor.Blazor/css/node-editor.css" />
   ```
4. Use the canvas component:
   ```razor
   @inject NodeEditorState EditorState
   <NodeEditorCanvas State="EditorState" />
   ```
```

### Custom Node Tutorial Outline
1. Create a class implementing `INodeContext`
2. Add methods with `[Node]` attribute
3. Define inputs/outputs via method parameters and return type
4. Register the context with DI
5. Nodes appear in context menu automatically

### API Reference Format
For each public class/interface:
- Purpose (one sentence)
- Constructor/parameters
- Properties
- Methods
- Events
- Example usage

## Checklist
- [x] Migration guide covers common WinForms patterns
- [x] Each public API has usage examples
- [x] Troubleshooting section covers top 5+ issues (covers 10)
- [x] Sample project builds and runs
- [x] Plugin guidance includes iOS restriction
- [x] Version-specific documentation
