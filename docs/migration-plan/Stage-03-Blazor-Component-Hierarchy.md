# Stage 03 — Blazor Component Hierarchy

## Goal
Implement the UI shell for the editor using Blazor components that replace WinForms controls and drawing.

## Deliverables
- Components: NodeEditorCanvas, NodeComponent, SocketComponent, ConnectionPath
- Scoped CSS for layout and visuals

## Tasks
1. Implement NodeEditorCanvas as the root container.
2. Render connections using SVG paths.
3. Render nodes using absolutely positioned divs.
4. Provide socket components for input/output pins.

## Acceptance Criteria
- Canvas renders nodes and connections from NodeEditorState.
- Visual structure matches existing WinForms layout at parity.

### Testing Parameters
- NUnit/xUnit visual smoke test renders 5+ nodes and 6+ connections without layout glitches.
- NUnit/xUnit snapshot test confirms DOM structure for NodeEditorCanvas, NodeComponent, ConnectionPath.

## Dependencies
Stage 02.

## Risks / Notes
- Ensure SVG scale matches CSS scale when zooming.
