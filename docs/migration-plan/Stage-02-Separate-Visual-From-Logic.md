# Stage 02 — Separate Visual from Logical Components

## Goal
Refactor core types so the graph’s data model is UI-agnostic and the visual state is isolated in view models.

## Deliverables
- Models: NodeData, SocketData, ConnectionData
- ViewModels: NodeViewModel, SocketViewModel
- State container: NodeEditorState

## Tasks
1. Extract data fields from Node/nSocket into NodeData/SocketData.
2. Move visual state (position, selection, size) into ViewModels.
3. Create NodeEditorState to hold Nodes, Connections, Selection, Zoom, Viewport.
4. Update usages inside execution logic to use data models (no UI references).

## Acceptance Criteria
- Data models contain no UI framework types.
- ViewModels provide all layout-related state.
- NodeManager logic can operate using data models plus view state.

### Testing Parameters
- NUnit/xUnit tests validate models are free of UI types (no System.Drawing/WinForms).
- NUnit/xUnit serialization tests confirm only DTOs are persisted.

## Dependencies
Stage 01.

## Risks / Notes
- Ensure existing serialization doesn’t embed UI-only fields.
