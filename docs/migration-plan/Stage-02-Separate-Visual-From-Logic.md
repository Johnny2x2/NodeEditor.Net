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

## Architecture Notes
Separate the *graph data* from the *view state* and *interaction state*:
- **Data (Models)**: `NodeData`, `SocketData`, `ConnectionData` — stable, serializable, no UI types.
- **ViewModels**: `NodeViewModel`, `SocketViewModel` — position, selection, size, hover state, etc.
- **State Container**: `NodeEditorState` — collections and session-level state (selection, zoom, viewport).

This enables:
- Serialization/persistence without UI noise.
- Testable execution logic (no UI framework coupling).
- Multiple UI renderers in the future (e.g., Web, MAUI, desktop).

## Detailed Tasks (Expanded)
1. **Model extraction**
   - Map `Node` → `NodeData` and `nSocket` → `SocketData`.
   - Normalize IDs into stable `string` identifiers (e.g., GUID).
2. **Connection normalization**
   - Explicit connection fields: `OutputNodeId`, `InputNodeId`, `OutputSocketName`, `InputSocketName`.
   - Avoid storing references to UI objects.
3. **ViewModel state**
   - Position/size, selection, and runtime view flags live in ViewModels.
   - Use `INotifyPropertyChanged` for live UI updates.
4. **Adapters (bridging legacy code)**
   - Introduce mappers to create `NodeData` from legacy `Node`.
   - Keep adapters isolated in a `Legacy` or `Adapters` folder so they can be removed later.
5. **State container**
   - `NodeEditorState` holds collections and editor-level state (zoom, viewport, selection).
   - Prefer `ObservableCollection<T>` for UI updates.

## Code Examples

### Model conversion adapter (temporary)
```csharp
public static class NodeAdapter
{
	public static NodeData ToNodeData(Node legacy)
	{
		var inputs = legacy.GetSockets()
			.Where(s => s.Input)
			.Select(s => new SocketData(
				s.Name,
				s.Type?.FullName ?? "System.Object",
				isInput: true,
				isExecution: s.IsMainExecution,
				value: s.Value))
			.ToList();

		var outputs = legacy.GetSockets()
			.Where(s => !s.Input)
			.Select(s => new SocketData(
				s.Name,
				s.Type?.FullName ?? "System.Object",
				isInput: false,
				isExecution: s.IsMainExecution,
				value: s.Value))
			.ToList();

		return new NodeData(
			legacy.GetGuid(),
			legacy.Name,
			legacy.Callable,
			legacy.ExecInit,
			inputs,
			outputs);
	}
}
```

### ViewModel creation
```csharp
var nodeVm = new NodeViewModel(nodeData)
{
	Position = new Point2D(100, 120),
	IsSelected = false
};
```

## Missing Architecture Gaps (to close in this stage)
- **Type resolution**: use a resolver for `SocketData.TypeName` to runtime types.
- **Value containers**: plan for `Value` serialization strategy (primitive vs complex payload).
- **Selection tracking**: maintain selection in `NodeEditorState` and mirror in each `NodeViewModel`.

## Checklist
- [ ] All models are UI-free and serializable
- [ ] ViewModels only contain UI-related state
- [ ] NodeManager logic can operate on models + state container
- [ ] Adapters are isolated and removable
