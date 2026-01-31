# Stage 10 — Integration & Testing

## Status: 🟡 Partially Complete

### What's Done
- ✅ Project reference from MAUI to NodeEditor.Blazor
- ✅ Services registered via `AddNodeEditor()` extension
- ✅ Test page in MAUI app (`Components/Pages/Home.razor`)
- ✅ Sample graph with 5 nodes and 5 connections
- ✅ CSS linked in `wwwroot/index.html`
- ✅ Windows build verified and running

### What's Remaining
- ❌ Full manual test checklist execution
- ❌ Mobile platform testing (Android, iOS)
- ❌ Touch gesture implementation
- ❌ Plugin loader iOS guard verification
- ❌ Error boundary integration

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

## Implementation Notes (for next developer)

### Current Integration
The MAUI app is set up in:
- `NodeEditorMax/MauiProgram.cs` - registers services via `AddNodeEditor()`
- `NodeEditorMax/Components/Pages/Home.razor` - test page with sample graph
- `NodeEditorMax/wwwroot/index.html` - includes CSS link
- `NodeEditorMax/wwwroot/app.css` - contains `.node-editor-container` full-screen style

### Running the App
```bash
# Windows
dotnet run --project NodeEditorMax/NodeEditorMax.csproj -f net10.0-windows10.0.19041.0

# Android (emulator or device)
dotnet run --project NodeEditorMax/NodeEditorMax.csproj -f net10.0-android

# iOS Simulator
dotnet run --project NodeEditorMax/NodeEditorMax.csproj -f net10.0-ios
```

### Manual Test Checklist
Run these tests on each platform:

#### Basic Rendering
- [ ] Canvas displays with dark background and grid
- [ ] All 5 sample nodes visible
- [ ] All 5 connections render as curves
- [ ] Socket dots show correct colors by type

#### Interaction
- [ ] Click node to select (blue border)
- [ ] Ctrl+click for multi-select
- [ ] Middle mouse button + drag to pan
- [ ] Mouse wheel to zoom in/out

#### Touch (Mobile)
- [ ] Single tap to select node
- [ ] Two-finger drag to pan (needs implementation)
- [ ] Pinch to zoom (needs implementation)

### Touch Gesture Implementation
For mobile, add touch gesture recognizers:
```csharp
// In NodeEditorCanvas.razor
@ontouchstart="OnTouchStart"
@ontouchmove="OnTouchMove"
@ontouchend="OnTouchEnd"

// Track touch points for pinch-zoom
private List<TouchPoint> _activeTouches = new();

private void OnTouchMove(TouchEventArgs e)
{
    if (e.Touches.Length == 2)
    {
        // Pinch zoom logic
        var distance = GetDistance(e.Touches[0], e.Touches[1]);
        var scale = distance / _initialPinchDistance;
        State.Zoom = _initialZoom * scale;
    }
    else if (e.Touches.Length == 1 && _isPanning)
    {
        // Single finger pan (or node drag)
    }
}
```

### Error Boundary
Wrap the canvas in an error boundary for graceful failure:
```razor
<ErrorBoundary>
    <ChildContent>
        <NodeEditorCanvas State="EditorState" />
    </ChildContent>
    <ErrorContent>
        <div class="error-panel">
            <h3>Something went wrong</h3>
            <button @onclick="Recover">Reload</button>
        </div>
    </ErrorContent>
</ErrorBoundary>
```

## Checklist
- [x] Editor renders in MAUI host
- [x] Core flows work on Windows
- [ ] Core flows work on Android
- [ ] Core flows work on iOS
- [ ] Plugin loader is skipped on iOS
- [ ] Touch gestures work on mobile
- [ ] Error boundary catches failures
