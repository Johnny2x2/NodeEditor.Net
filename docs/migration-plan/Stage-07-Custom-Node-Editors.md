# Stage 07 — Custom Node Editors

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

## Checklist
- [ ] Editor contract stable and documented
- [ ] Editor updates are reflected within one render cycle
- [ ] Default editors cover basic socket types
