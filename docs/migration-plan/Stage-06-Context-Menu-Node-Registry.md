# Stage 06 — Context Menu & Node Registry

## Status: 🔴 Not Started

### What's Done
- ✅ `NodeData` model supports all node properties
- ✅ `NodeAttribute` exists in legacy project (needs port)
- ✅ Canvas component can receive new nodes via `EditorState.AddNode()`

### What's Remaining
- ❌ `NodeRegistryService` - scan and catalog nodes
- ❌ `NodeDefinition` model - schema for node templates
- ❌ `ContextMenuComponent` - right-click menu UI
- ❌ Assembly scanning for `[Node]` attributes
- ❌ Search and category filtering

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
- **Node ID policy**: stable IDs across sessions (consider GUID or hash-based)
- **Registry caching**: avoid scanning assemblies more than once
- **Search ranking**: prioritize exact matches and category matches

## Implementation Notes (for next developer)

### Legacy Code Reference
- `NodeEditor/NodeAttribute.cs` - Attribute for marking node methods
- `NodeEditor/StandardNodeContext.cs` (and partials) - Built-in node implementations
- Legacy uses reflection to find methods with `[Node]` attribute on `INodesContext` implementations

### Node Discovery Strategy
1. Define `INodeContext` interface (Blazor equivalent of `INodesContext`)
2. Scan assemblies for types implementing `INodeContext`
3. Find methods with `[Node]` attribute
4. Build `NodeDefinition` from attribute metadata + method signature

### Recommended Files to Create
```
NodeEditor.Blazor/Services/Registry/
├── NodeRegistryService.cs       # Main registry, scans and caches
├── NodeDefinition.cs            # Template for node types
├── NodeDiscoveryService.cs      # Assembly scanning
├── INodeContext.cs              # Interface for node implementations
└── NodeCatalog.cs               # Searchable, categorized collection

NodeEditor.Blazor/Components/
├── ContextMenu.razor            # Right-click menu container
├── ContextMenuItem.razor        # Individual menu item
└── ContextMenuSearch.razor      # Search input component
```

### Context Menu Positioning
Use graph coordinates from right-click event:
```csharp
private void OnContextMenu(MouseEventArgs e)
{
    var graphPos = _coordinator.ScreenToGraph(new Point2D(e.ClientX, e.ClientY));
    _contextMenuPosition = graphPos;
    _showContextMenu = true;
}
```

### Node Factory Pattern
```csharp
// NodeDefinition includes a factory that creates NodeData
public Func<string, NodeData> CreateFactory(MethodInfo method, NodeAttribute attr)
{
    return (id) => new NodeData(
        Id: id,
        Name: attr.Name ?? method.Name,
        Callable: attr.IsExecutable,
        ExecInit: attr.IsInitializer,
        Inputs: BuildInputs(method, attr),
        Outputs: BuildOutputs(method, attr));
}
```

## Checklist
- [ ] Registry loads nodes from primary assembly
- [ ] Context menu is keyboard navigable
- [ ] Plugin nodes appear only on supported platforms
- [ ] Search filters nodes in real-time
- [ ] Categories group related nodes
- [ ] Node added at cursor position in graph coordinates
