# Stage 11 — Performance Optimization

## Status: 🔴 Not Started

### What's Done
- ✅ `@key` used on node and connection loops
- ✅ CSS `will-change: transform` on viewport

### What's Remaining
- ❌ Viewport culling (only render visible nodes)
- ❌ Connection batching (single SVG path for all connections)
- ❌ `ShouldRender` overrides in components
- ❌ Frame budget metrics / diagnostics overlay
- ❌ Memory profiling

## Goal
Ensure large graphs perform well in Blazor and MAUI.

## Deliverables
- Render throttling or batching
- Viewport culling
- Reduced re-rendering

## Tasks
1. Use @key to stabilize list rendering.
2. Add viewport-based culling for nodes/connections.
3. Override ShouldRender where appropriate.
4. Minimize JS interop calls.

## Acceptance Criteria
- Smooth interactions with 500+ nodes.
- Memory usage remains stable after extended use.

### Testing Parameters
- NUnit/xUnit performance test maintains 60 FPS with 500 nodes and 800 connections.
- NUnit/xUnit soak test for 30 minutes shows no memory growth beyond 5%.

## Dependencies
Stage 10.

## Risks / Notes
- Be careful not to break hit testing when culling.

## Architecture Notes
Performance tuning must be **data-driven**. Add optional diagnostic counters for:
- Render count per frame
- Connection path recalculations
- Hit-test cost

## Detailed Tasks (Expanded)
1. **Virtualization**
	- Only render nodes within the viewport.
2. **Connection batching**
	- Render connections in a single SVG where possible.
3. **State diffing**
	- Reduce re-renders by comparing last render state.
4. **Caching**
	- Cache path calculations and node bounds.
5. **JS interop minimization**
	- Only use JS for pointer capture if strictly necessary.

## Code Examples

### Viewport culling
```csharp
var visibleNodes = State.Nodes.Where(n =>
	 HitTest.Contains(Viewport, new Point2D(n.Position.X, n.Position.Y)));
```

## Missing Architecture Gaps (to close in this stage)
- **Frame budget metrics** displayed in a dev overlay
- **Batch updates** when moving multiple nodes

## Implementation Notes (for next developer)

### Performance Target
- 60 FPS with 500 nodes and 800 connections
- Memory stable after 30 minutes of use

### Viewport Culling Strategy
Only render nodes that intersect the visible viewport:
```csharp
// In NodeEditorCanvas.razor
private IEnumerable<NodeViewModel> VisibleNodes
{
    get
    {
        var viewportRect = CalculateVisibleGraphRect();
        return State.Nodes.Where(n => IntersectsViewport(n, viewportRect));
    }
}

private bool IntersectsViewport(NodeViewModel node, Rect2D viewport)
{
    var nodeRect = new Rect2D(
        node.Position.X, node.Position.Y,
        node.Size.Width, node.Size.Height);
    return RectIntersects(nodeRect, viewport);
}

private Rect2D CalculateVisibleGraphRect()
{
    // Use CoordinateConverter to map screen viewport to graph coordinates
    return _converter.ScreenToGraph(new Rect2D(0, 0, _canvasWidth, _canvasHeight));
}
```

### Connection Visibility
A connection is visible if either endpoint is visible OR the path crosses the viewport:
```csharp
private bool IsConnectionVisible(ConnectionData conn, Rect2D viewport)
{
    var sourceNode = State.Nodes.FirstOrDefault(n => n.Data.Id == conn.OutputNodeId);
    var targetNode = State.Nodes.FirstOrDefault(n => n.Data.Id == conn.InputNodeId);
    
    if (sourceNode == null || targetNode == null) return false;
    
    // Visible if either node is visible
    if (IntersectsViewport(sourceNode, viewport)) return true;
    if (IntersectsViewport(targetNode, viewport)) return true;
    
    // Or if the bezier path crosses the viewport (more complex)
    return BezierIntersectsRect(GetBezierPath(conn), viewport);
}
```

### ShouldRender Optimization
Prevent unnecessary renders in components:
```csharp
// In NodeComponent.razor.cs
private Point2D _lastPosition;
private bool _lastSelected;

protected override bool ShouldRender()
{
    if (Node.Position != _lastPosition || Node.IsSelected != _lastSelected)
    {
        _lastPosition = Node.Position;
        _lastSelected = Node.IsSelected;
        return true;
    }
    return false;
}
```

### Diagnostics Overlay
Create a dev-only overlay showing:
- Current FPS
- Rendered node count
- Rendered connection count
- Memory usage

```razor
@if (ShowDiagnostics)
{
    <div class="ne-diagnostics">
        <span>FPS: @_fps</span>
        <span>Nodes: @VisibleNodes.Count() / @State.Nodes.Count</span>
        <span>Connections: @VisibleConnections.Count() / @State.Connections.Count</span>
    </div>
}
```

### Benchmarking Test
Create a performance test that:
1. Generates 500 random nodes
2. Generates 800 random connections
3. Measures render time
4. Measures pan/zoom frame rate

## Checklist
- [ ] 500+ nodes stay interactive at 60 FPS
- [ ] No GC spikes on drag
- [ ] Connection render time is stable
- [ ] Memory usage stable over 30 minutes
- [ ] Viewport culling reduces render count
- [ ] Diagnostics overlay available in dev builds
