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

## Architecture Notes
Define a **versioned persistence schema** that is independent of UI framework types.
Persist:
- Node definitions (ID, type, parameters)
- Node view state (position, size, selection) in a dedicated view DTO
- Connection endpoints and execution flags

## Detailed Tasks (Expanded)
1. **DTO schema**
	- `GraphDto` with `Version`, `Nodes`, `Connections`, `Viewport`, `Zoom`.
2. **Value serialization**
	- Use `JsonElement` or a custom converter for socket values.
3. **Versioning**
	- Implement schema version upgrades (e.g., v1 → v2).
4. **File format**
	- JSON default; optional XML adapter for legacy.

## Code Examples

### DTO schema
```csharp
public sealed record class GraphDto(
	 int Version,
	 List<NodeDto> Nodes,
	 List<ConnectionDto> Connections,
	 Rect2D Viewport,
	 double Zoom);

public sealed record class NodeDto(
	 string Id,
	 string Name,
	 double X,
	 double Y,
	 Dictionary<string, object?> SocketValues);
```

### Save/load
```csharp
var json = JsonSerializer.Serialize(graphDto, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText(path, json);

var loaded = JsonSerializer.Deserialize<GraphDto>(File.ReadAllText(path));
```

## Missing Architecture Gaps (to close in this stage)
- **Type resolution** for socket values when deserializing
- **Validation** of connection endpoints
- **Backward compatibility** for older graph files

## Checklist
- [ ] Round-trip retains graph fidelity
- [ ] Schema versioning works for upgrades
- [ ] No UI references in serialized JSON
