# Stage 01 — Project Setup & Core Infrastructure

## Goal
Stand up a new MAUI Hybrid Blazor–ready class library and define platform-agnostic primitives to replace System.Drawing and WinForms types.

## Inputs
- Existing WinForms library project: NodeEditor/NodeEditor.csproj
- MAUI host app: NodeEditorMax/NodeEditorMax.csproj

## Deliverables
- New library project: NodeEditor.Blazor
- Base folders: Components, Models, Services, wwwroot (css/js)
- Cross-platform primitives: Point2D, Size2D, Rect2D, ColorValue, StrokeStyle
- Platform guard utilities for plugin loading (iOS-disabled).

## Tasks
1. Create NodeEditor.Blazor class library targeting net10.0 with Razor SDK.
2. Add minimal _Imports.razor and wwwroot scaffolding.
3. Add platform-agnostic geometry and style primitives.
4. Establish solution references (do not wire into MAUI yet).

## Acceptance Criteria
- Project builds without WinForms or System.Drawing references.
- Geometry and style primitives compile and are used by at least one placeholder type.

### Testing Parameters
- NUnit/xUnit build test passes on Windows with no WinForms/System.Drawing references.
- NUnit/xUnit smoke test ensures `dotnet build` emits no platform API warnings.

## Dependencies
None.

## Risks / Notes
- Avoid pulling in System.Drawing.Common to keep cross-platform compliance.
