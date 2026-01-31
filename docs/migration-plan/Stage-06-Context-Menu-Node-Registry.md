# Stage 06 — Context Menu & Node Registry

## Goal
Rebuild the node discovery and context menu using Blazor components.

## Deliverables
- NodeRegistryService
- ContextMenu component with categories

## Tasks
1. Port reflection-based node discovery into NodeRegistryService.
2. Build a right-click context menu to add nodes.
3. Provide grouping and search (optional).
4. Add plugin registration entry points (for non-iOS platforms).

## Acceptance Criteria
- Right-click opens menu with categorized nodes.
- Selecting a node adds it at cursor position.
- Registry supports registering nodes from plugins (desktop/Android/Mac Catalyst).

### Testing Parameters
- NUnit/xUnit test: menu opens within 50ms on right-click.
- NUnit/xUnit registry test ensures all annotated nodes are discoverable.
- NUnit/xUnit test ensures plugin registry merges categories correctly (non-iOS).

## Dependencies
Stage 03.

## Risks / Notes
- Ensure menu positioning respects viewport and zoom.
