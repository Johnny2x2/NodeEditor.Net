# Stage 04 — Interaction Logic

## Goal
Port mouse, keyboard, zoom, and drag behaviors from WinForms to Blazor event handlers.

## Deliverables
- Input handlers for selection, drag, connect, pan, and zoom
- Connection drag state machine

## Tasks
1. Add mouse handlers to NodeEditorCanvas for select, drag, pan.
2. Implement socket drag start/drag end for connections.
3. Add keyboard handlers (delete, escape, etc.).
4. Add zoom handling (mouse wheel + transform).

## Acceptance Criteria
- Nodes can be moved with drag.
- Connections can be created and canceled reliably.
- Zoom/pan preserves hit testing.

### Testing Parameters
- NUnit/xUnit drag test: moving a node updates its position within 16ms per frame budget.
- NUnit/xUnit connection test: invalid type connections are rejected 100% of attempts.
- NUnit/xUnit zoom test: socket hit testing works at 0.5x, 1x, and 2x.

## Dependencies
Stage 03.

## Risks / Notes
- Consider pointer capture via JS interop for reliable drag.
