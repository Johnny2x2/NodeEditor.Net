# Stage 09 — Serialization & Persistence

## Goal
Provide graph save/load support without WinForms types.

## Deliverables
- DTOs for graph persistence
- JSON serialization (and optional XML compatibility)

## Tasks
1. Create DTOs for nodes, sockets, connections, viewport.
2. Implement JSON save/load (System.Text.Json).
3. Optionally add XML compatibility for legacy graphs.

## Acceptance Criteria
- Graphs can be saved and restored with full fidelity.
- No UI types are serialized.

### Testing Parameters
- NUnit/xUnit round-trip tests for JSON preserve node positions, connections, and values.
- NUnit/xUnit backward-compatibility test loads a legacy XML graph (if supported).

## Dependencies
Stage 02.

## Risks / Notes
- Validate type name/versioning strategy for node definitions.
