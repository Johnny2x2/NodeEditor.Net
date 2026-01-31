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

## Architecture Notes
The Blazor UI should be **stateless where possible**, receiving `NodeEditorState` and `ViewModel` collections as parameters.
Recommended component hierarchy:
- `NodeEditorCanvas` (root, handles transforms, viewport, pointer capture)
  - `ConnectionLayer` (SVG)
	- `ConnectionPath`
  - `NodeLayer` (HTML)
	- `NodeComponent`
	  - `SocketComponent` (inputs/outputs)

Keep **render logic** and **interaction logic** separate. Interactions should dispatch to a controller/state service.

## Detailed Tasks (Expanded)
1. **Root canvas**
   - `NodeEditorCanvas` uses a container div with `position: relative` and `overflow: hidden`.
   - Apply zoom/pan via CSS transform on an inner viewport element.
2. **Connection rendering**
   - Use SVG for lines; keep SVG size bound to viewport.
   - Compute path segments based on socket positions (Bezier curves).
3. **Node rendering**
   - Use absolutely positioned elements based on `NodeViewModel.Position`.
   - Set width/height from `NodeViewModel.Size`.
4. **Socket components**
   - Render as small circles/ports, with data attributes for hit testing.
   - Expose events for connection drag.

## Code Examples

### NodeEditorCanvas skeleton
```razor
@inherits OwningComponentBase

<div class="ne-canvas" @onpointerdown="OnPointerDown" @onwheel="OnWheel">
	<div class="ne-viewport" style="transform: translate(@PanXpx, @PanYpx) scale(@Zoom)">
		<svg class="ne-connections">
			@foreach (var connection in State.Connections)
			{
				<ConnectionPath Connection="connection" />
			}
		</svg>

		@foreach (var node in State.Nodes)
		{
			<NodeComponent Node="node" />
		}
	</div>
</div>
```

### NodeComponent positioning
```razor
<div class="ne-node" style="left:@Node.Position.Xpx; top:@Node.Position.Ypx; width:@Node.Size.Widthpx; height:@Node.Size.Heightpx">
	<div class="ne-node-header">@Node.Data.Name</div>
	<div class="ne-node-body">
		@foreach (var input in Node.Inputs)
		{
			<SocketComponent Socket="input" />
		}
		@foreach (var output in Node.Outputs)
		{
			<SocketComponent Socket="output" />
		}
	</div>
</div>
```

## Missing Architecture Gaps (to close in this stage)
- ~~**Coordinate conversion utilities**: screen ↔ graph coordinates for hit testing.~~ ✅ Implemented in `CoordinateConverter.cs`
- ~~**Layout system**: node internal layout (header, sockets, editor area) with consistent padding.~~ ✅ Implemented in `node-editor.css`
- ~~**Connection path router**: centralized function that converts socket positions into SVG path strings.~~ ✅ Implemented in `ConnectionPath.razor`

## Checklist
- [x] NodeEditorCanvas renders both SVG and HTML layers
- [x] NodeComponent and SocketComponent render with stable keys
- [x] No UI logic in models; view models only

## Implementation Summary (Completed)

### Components Created
| Component | File | Description |
|-----------|------|-------------|
| `NodeEditorCanvas` | `Components/NodeEditorCanvas.razor` | Root container with pan/zoom viewport, pointer handling, and cascading state |
| `NodeComponent` | `Components/NodeComponent.razor` | Absolutely positioned node with header, inputs, and outputs |
| `SocketComponent` | `Components/SocketComponent.razor` | Input/output pins with type-based coloring |
| `ConnectionPath` | `Components/ConnectionPath.razor` | SVG Bezier curve connections between sockets |

### Services Added
| Service | File | Description |
|---------|------|-------------|
| `CoordinateConverter` | `Services/CoordinateConverter.cs` | Screen ↔ graph coordinate conversion for zoom/pan |

### Styles
| File | Description |
|------|-------------|
| `wwwroot/css/node-editor.css` | Full CSS for canvas, nodes, sockets, and connections |

### Tests
| Test Class | Tests | Description |
|------------|-------|-------------|
| `ComponentHierarchyTests` | 9 | Smoke tests for nodes, connections, coordinates, and selection |
