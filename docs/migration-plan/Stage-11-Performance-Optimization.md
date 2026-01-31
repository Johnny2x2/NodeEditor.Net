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
