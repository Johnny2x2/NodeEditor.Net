# Stage 11 — Performance Optimization

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

## Checklist
- [ ] 500+ nodes stay interactive
- [ ] No GC spikes on drag
- [ ] Connection render time is stable
