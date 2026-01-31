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

## Architecture Notes
Separate **registry** from **UI**:
- `NodeRegistryService` loads node definitions and exposes read-only catalogs.
- `ContextMenu` UI only binds to registry data and dispatches add-node actions.

## Detailed Tasks (Expanded)
1. **Registry model**
	- Define `NodeDefinition` (Name, Category, Description, Inputs, Outputs, Factory).
2. **Discovery**
	- Scan assemblies for `[Node]` attributes or a custom interface.
3. **Context menu**
	- Right-click opens menu at cursor.
	- Search box filters results.
4. **Add node action**
	- Instantiate `NodeData` and `NodeViewModel`.
	- Place node at cursor in graph coordinates.
5. **Plugin integration**
	- Registry can merge plugin nodes if platform supports.

## Code Examples

### Registry model
```csharp
public sealed record class NodeDefinition(
	 string Id,
	 string Name,
	 string Category,
	 string Description,
	 IReadOnlyList<SocketData> Inputs,
	 IReadOnlyList<SocketData> Outputs,
	 Func<NodeData> Factory);
```

### Context menu add-node flow
```csharp
void AddNode(NodeDefinition definition, Point2D cursorGraphPoint)
{
	 var nodeData = definition.Factory();
	 var nodeVm = new NodeViewModel(nodeData)
	 {
		  Position = cursorGraphPoint
	 };

	 EditorState.Nodes.Add(nodeVm);
}
```

## Missing Architecture Gaps (to close in this stage)
- **Node ID policy**: stable IDs across sessions
- **Registry caching**: avoid scanning assemblies more than once
- **Search ranking**: prioritize exact matches and category matches

## Checklist
- [ ] Registry loads nodes from primary assembly
- [ ] Context menu is keyboard navigable
- [ ] Plugin nodes appear only on supported platforms
