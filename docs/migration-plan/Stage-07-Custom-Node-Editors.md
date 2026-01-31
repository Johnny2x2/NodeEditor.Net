# Stage 07 — Custom Node Editors

## Status: 🔴 Not Started

### What's Done
- ✅ `SocketData` has `Value` property (`SocketValue?`)
- ✅ `SocketValue` supports type-safe value storage
- ✅ `SocketComponent` renders socket with type info

### What's Remaining
- ❌ `INodeCustomEditor` interface contract
- ❌ `NodeEditorRegistry` service for editor lookup
- ❌ Default editors (text, number, bool, color)
- ❌ Socket-to-editor binding in `SocketComponent`
- ❌ Focus management between editors and canvas

## Goal
Replace WinForms custom editors with Blazor-based editors.

## Deliverables
- INodeCustomEditor interface
- Example editors (text, number, bool)

## Tasks
1. Define custom editor contract using RenderFragment.
2. Implement basic editor components.
3. Wire custom editor rendering into NodeComponent.

## Acceptance Criteria
- Nodes render custom editors where defined.
- Editor values update node socket data.

### Testing Parameters
- NUnit/xUnit test: editor input changes propagate to socket values within one render cycle.
- NUnit/xUnit snapshot test verifies editor layout for text/number/bool editors.

## Dependencies
Stage 03.

## Risks / Notes
- Keep editors lightweight to avoid excessive re-render.

## Architecture Notes
Custom editors should be **opt-in per socket** and rendered via a shared interface.
Recommended approach:
- `INodeCustomEditor` returns a `RenderFragment` using context info (node, socket, value binding).
- Editors update `SocketData.Value` and notify observers.

## Detailed Tasks (Expanded)
1. **Define editor contract**
	- Provide metadata (supported types, label, size).
2. **Standard editors**
	- Text input, numeric input, checkbox, color picker (optional).
3. **Bind values**
	- Update socket values in `NodeData` with validation.
4. **Render pipeline**
	- `NodeComponent` chooses the editor based on socket type.

## Code Examples

### Editor contract
```csharp
public interface INodeCustomEditor
{
	 bool CanEdit(SocketData socket);
	 RenderFragment Render(SocketEditorContext context);
}
```

### Example text editor
```razor
<input class="ne-editor-text" value="@Context.Value" @oninput="OnInput" />

@code {
	 [Parameter] public SocketEditorContext Context { get; set; } = default!;

	 private void OnInput(ChangeEventArgs e)
	 {
		  Context.SetValue(e.Value?.ToString());
	 }
}
```

## Missing Architecture Gaps (to close in this stage)
- **Validation strategy**: how to validate values and show errors
- **Editor registry**: a service to register editors by socket type
- **Focus management**: move focus between editors and canvas

## Implementation Notes (for next developer)

### When to Show Editors
Editors should appear for **input sockets that are not connected**. If an input socket has an incoming connection, its value comes from the connected output, so no editor is shown.

### SocketComponent Modification
Update `SocketComponent.razor` to conditionally render an editor:
```razor
@if (Socket.Data.IsInput && !IsConnected)
{
    <div class="ne-socket-editor">
        @EditorRegistry.GetEditor(Socket.Data.TypeName)?.Render(CreateContext())
    </div>
}
```

### Default Editor Components to Create
```
NodeEditor.Blazor/Components/Editors/
├── TextEditor.razor         # string input
├── NumericEditor.razor      # int, float, double
├── BoolEditor.razor         # checkbox
├── ColorEditor.razor        # color picker (optional)
├── DropdownEditor.razor     # enum values
└── EditorBase.razor         # shared base class
```

### Editor Context
```csharp
public sealed class SocketEditorContext
{
    public required SocketViewModel Socket { get; init; }
    public required NodeViewModel Node { get; init; }
    public required Action<object?> SetValue { get; init; }
    public object? Value => Socket.Data.Value?.GetValue<object>();
}
```

### Value Update Flow
1. User edits value in editor component
2. Editor calls `Context.SetValue(newValue)`
3. SetValue updates `SocketData.Value` via immutable record `with` pattern
4. State change propagates to UI

### Preventing Canvas Interactions
Editors should stop propagation of pointer events to prevent node selection/drag when clicking in an input field:
```razor
<input @onclick:stopPropagation @onpointerdown:stopPropagation ... />
```

## Checklist
- [ ] Editor contract stable and documented
- [ ] Editor updates are reflected within one render cycle
- [ ] Default editors cover basic socket types (string, int, float, bool)
- [ ] Connected inputs hide their editors
- [ ] Focus in editor doesn't trigger canvas interactions
- [ ] Validation errors displayed inline
