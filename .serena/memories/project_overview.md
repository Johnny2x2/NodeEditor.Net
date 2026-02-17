# NodeEditorMax overview

Purpose: NodeEditorMax is a .NET node editor/visual scripting framework with a legacy WinForms implementation (NodeEditor) and a new Blazor-based UI library plus MAUI host (NodeEditor.Blazor + NodeEditorMax). The repo contains a staged migration plan in docs/migration-plan.

Tech stack:
- C#/.NET (multi-project solution)
- Blazor component library (NodeEditor.Blazor)
- MAUI host app (NodeEditorMax)
- Test project: NodeEditor.Blazor.Tests (xUnit)

Repo structure (high-level):
- NodeEditor/: legacy WinForms node editor and execution engine
- NodeEditor.Blazor/: new models, services, components
- NodeEditor.Blazor.Tests/: tests for Blazor library
- NodeEditor.Blazor.WebHost/: web host for running Blazor components
- NodeEditorMax/: MAUI host app
- docs/migration-plan/: staged migration documentation
