# Stage 04 — Interaction Logic

## Status: ✅ Complete

### What's Done
- ✅ Pan via middle mouse button (`NodeEditorCanvas.razor`)
- ✅ Zoom via mouse wheel with min/max limits
- ✅ Node selection (click to select, Ctrl+click for multi-select)
- ✅ `CoordinateConverter` service for screen ↔ graph conversions
- ✅ Connection drag start/end events wired to sockets
- ✅ Basic connection preview (pending connection)
- ✅ Node dragging (graph-space delta, multi-select support)
- ✅ Connection type validation (execution/type compatibility)
- ✅ Keyboard shortcuts (Delete, Escape, Ctrl+A, Ctrl+Z/Ctrl+Y)
- ✅ Selection box (rubber band selection)
- ✅ Undo/redo hooks (placeholder events)

### What's Remaining
- ✅ None

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

## Architecture Notes
Use an **interaction controller** (service or controller class) to manage pointer state:
- Dragging nodes
- Panning canvas
- Connection dragging
- Selection box

Keep interaction **state** in a single struct/class to reduce UI jitter and event ordering bugs.

## Detailed Tasks (Expanded)
1. **Pointer capture and tracking**
	- Use `@onpointerdown`, `@onpointermove`, `@onpointerup` with capture when supported.
2. **Selection logic**
	- Single click selects; shift adds/removes from selection.
	- Drag selection box updates a temporary selection list.
3. **Drag logic**
	- Node drag uses graph-space delta (respect zoom).
	- Canvas pan uses screen delta.
4. **Connection drag**
	- Start at socket → preview path → commit on compatible target.
5. **Keyboard shortcuts**
	- Delete: remove nodes/connections
	- Escape: cancel drag/selection
	- Ctrl+A: select all

## Code Examples

### Interaction state
```csharp
public sealed class InteractionState
{
	 public bool IsPanning { get; set; }
	 public bool IsDraggingNode { get; set; }
	 public string? DraggingNodeId { get; set; }
	 public bool IsConnecting { get; set; }
	 public string? FromSocketId { get; set; }
	 public Point2D PointerStart { get; set; }
	 public Point2D PointerCurrent { get; set; }
}
```

### Hit testing utilities
```csharp
public static class HitTest
{
	 public static bool Contains(Rect2D rect, Point2D p) =>
		  p.X >= rect.X && p.X <= rect.X + rect.Width &&
		  p.Y >= rect.Y && p.Y <= rect.Y + rect.Height;
}
```

## Missing Architecture Gaps (to close in this stage)
- ~~**Graph coordinate conversions**: `ScreenToGraph(Point2D)` and `GraphToScreen(Point2D)` helpers.~~ ✅ Done in `CoordinateConverter.cs`
- **Type compatibility**: connection validation strategy based on socket type + execution/data rules.
- **Undo/redo hooks**: placeholder events for future history (even if not implemented yet).
- **Node drag controller**: separate drag state from canvas, apply delta to node positions

## Implementation Notes (for next developer)

### Current Code Location
- Canvas pointer handlers: `NodeEditor.Blazor/Components/NodeEditorCanvas.razor` (lines 107-150)
- Coordinate conversion: `NodeEditor.Blazor/Services/CoordinateConverter.cs`
- Socket events: `NodeEditor.Blazor/Components/NodeEditorCanvas.razor.cs` (`SocketPointerEventArgs`)

### To Implement Node Dragging
The canvas already tracks `_isDraggingNode` and `_draggingNode` but the `NodeComponent` needs to:
1. Emit a `OnNodeDragStart` event when left-click on node header
2. Canvas captures pointer and applies delta via `CoordinateConverter.ScreenDeltaToGraph()`
3. Update `node.Position` on each `OnPointerMove`

### To Implement Connection Validation
Create a `ConnectionValidator` service:
```csharp
public class ConnectionValidator
{
    public bool CanConnect(SocketData source, SocketData target)
    {
        // Rule 1: Can't connect to self
        // Rule 2: Execution must match execution
        // Rule 3: Data types must be compatible (or object)
        // Rule 4: Input connects to output only
    }
}
```

### To Implement Keyboard Shortcuts
Add `@onkeydown` to canvas with `tabindex="0"`:
- Delete/Backspace: Remove selected nodes and their connections
- Escape: Cancel current operation (drag, connection)
- Ctrl+A: Select all nodes

## Checklist
- [x] Dragging respects zoom (via CoordinateConverter)
- [x] Selection is deterministic and consistent
- [x] Connection preview renders at 60 FPS
- [x] Node drag works smoothly
- [x] Keyboard shortcuts implemented
- [x] Connection type validation
