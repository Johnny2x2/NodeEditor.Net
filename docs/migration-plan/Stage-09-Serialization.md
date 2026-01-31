# Stage 09 — Serialization & Persistence

## Status: 🔴 Not Started

### What's Done
- ✅ `SocketValue` has JSON serialization support (`SocketValueConverter`)
- ✅ All models are immutable records (easy to serialize)
- ✅ `NodeData`, `ConnectionData` use primitives only (no UI types)

### What's Remaining
- ❌ `GraphDto` root schema with version
- ❌ `NodeDto`, `ConnectionDto`, `ViewportDto` models
- ❌ `GraphSerializer` service (save/load)
- ❌ Schema versioning and migration
- ❌ Legacy XML import (optional)

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
- **Validation** of connection endpoints (nodes exist, sockets exist)
- **Backward compatibility** for older graph files

## Implementation Notes (for next developer)

### Serialization Strategy
Use `System.Text.Json` with source generators for AOT compatibility:
```csharp
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(GraphDto))]
public partial class GraphSerializerContext : JsonSerializerContext { }
```

### DTO Schema
```csharp
public sealed record GraphDto(
    int Version,
    List<NodeDto> Nodes,
    List<ConnectionDto> Connections,
    ViewportDto Viewport);

public sealed record NodeDto(
    string Id,
    string TypeId,           // Reference to NodeDefinition
    string Name,
    double X,
    double Y,
    double Width,
    double Height,
    Dictionary<string, JsonElement> SocketValues);

public sealed record ConnectionDto(
    string OutputNodeId,
    string OutputSocketName,
    string InputNodeId,
    string InputSocketName,
    bool IsExecution);

public sealed record ViewportDto(
    double X,
    double Y,
    double Width,
    double Height,
    double Zoom);
```

### GraphSerializer Service
```csharp
public sealed class GraphSerializer
{
    public GraphDto Export(NodeEditorState state)
    {
        return new GraphDto(
            Version: 1,
            Nodes: state.Nodes.Select(ToDto).ToList(),
            Connections: state.Connections.Select(ToDto).ToList(),
            Viewport: new ViewportDto(
                state.Viewport.X, state.Viewport.Y,
                state.Viewport.Width, state.Viewport.Height,
                state.Zoom));
    }

    public void Import(NodeEditorState state, GraphDto dto)
    {
        state.Clear(); // Need to add Clear() method
        foreach (var nodeDto in dto.Nodes)
        {
            state.AddNode(FromDto(nodeDto));
        }
        foreach (var connDto in dto.Connections)
        {
            state.AddConnection(FromDto(connDto));
        }
        state.Viewport = new Rect2D(
            dto.Viewport.X, dto.Viewport.Y,
            dto.Viewport.Width, dto.Viewport.Height);
        state.Zoom = dto.Viewport.Zoom;
    }
}
```

### File Operations
```csharp
// Save
var dto = _serializer.Export(_state);
var json = JsonSerializer.Serialize(dto, GraphSerializerContext.Default.GraphDto);
await File.WriteAllTextAsync(path, json);

// Load
var json = await File.ReadAllTextAsync(path);
var dto = JsonSerializer.Deserialize(json, GraphSerializerContext.Default.GraphDto);
_serializer.Import(_state, dto);
```

### Schema Versioning
When loading, check version and apply migrations:
```csharp
if (dto.Version < CurrentVersion)
{
    dto = MigrateSchema(dto);
}
```

## Checklist
- [ ] Round-trip retains graph fidelity
- [ ] Schema versioning works for upgrades
- [ ] No UI references in serialized JSON
- [ ] Socket values serialize correctly (all types)
- [ ] Validation on load (missing nodes/sockets)
- [ ] Auto-save support (optional)
