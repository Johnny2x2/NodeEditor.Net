# Stage 10 — Integration & Testing

## Goal
Integrate the new library into the MAUI host and validate key workflows.

## Deliverables
- Project reference to NodeEditor.Blazor
- Test page in MAUI app
- Manual test checklist

## Tasks
1. Reference NodeEditor.Blazor from NodeEditorMax.
2. Add a test page using NodeEditorCanvas.
3. Verify core flows: add, connect, move, execute, save/load.
4. Verify plugin loader is disabled on iOS builds.

## Acceptance Criteria
- Editor works on Windows and at least one mobile target.
- No WinForms dependencies in the app.
- iOS build skips plugin loading without errors.

### Testing Parameters
- NUnit/xUnit smoke test on Windows + Android/iOS completes without crashes.
- NUnit/xUnit dependency scan confirms no WinForms/System.Drawing usage in the MAUI app.
- NUnit/xUnit iOS guard test confirms plugin loading is short-circuited.

## Dependencies
Stages 03–09.

## Risks / Notes
- Validate touch inputs on mobile platforms.

## Architecture Notes
Integration should keep **all core logic** in `NodeEditor.Blazor` and host only in `NodeEditorMax`.
The MAUI app should supply:
- Page shell
- Services registration (from Stage 2 onward)
- UI hosting via BlazorWebView

## Detailed Tasks (Expanded)
1. **Reference library**
	- Add project reference from MAUI app.
2. **Add test page**
	- Create a test route and render `NodeEditorCanvas` with sample data.
3. **Build config**
	- Ensure correct `wwwroot` and static assets are included.
4. **Platform guards**
	- Verify plugin loading is disabled on iOS builds.
5. **Manual test checklist**
	- Add nodes, move, connect, execute, save/load

## Code Examples

### MAUI page integration
```razor
@page "/editor"
<NodeEditorCanvas State="EditorState" />

@code {
	 [Inject] public NodeEditorState EditorState { get; set; } = default!;
}
```

## Missing Architecture Gaps (to close in this stage)
- **Sample data provider** for test pages
- **Error surface** for execution/logging
- **Touch-specific gestures** (pinch zoom, two-finger pan)

## Checklist
- [ ] Editor renders in MAUI host
- [ ] Core flows work on at least two platforms
- [ ] Plugin loader is skipped on iOS
