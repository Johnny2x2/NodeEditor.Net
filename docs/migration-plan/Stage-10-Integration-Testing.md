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
