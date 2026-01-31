# Stage 12 — Documentation & Migration Guide

## Goal
Provide clear guidance for users migrating from WinForms to MAUI Blazor.

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

## Checklist
- [ ] Migration guide covers common WinForms patterns
- [ ] Each public API has usage examples
- [ ] Troubleshooting section covers top 5 issues
