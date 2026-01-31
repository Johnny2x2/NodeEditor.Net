# NodeEditor Migration Plan — Stages

## Current Status (Updated: January 2026)

| Stage | Title | Status | Notes |
|-------|-------|--------|-------|
| 01 | Project Setup & Core Infrastructure | ✅ Complete | Cross-platform primitives, no WinForms refs |
| 02 | Separate Visual from Logical Components | ✅ Complete | Models, ViewModels, State with events |
| 03 | Blazor Component Hierarchy | ✅ Complete | Canvas, Node, Socket, Connection components |
| 04 | Interaction Logic | 🟡 Partial | Basic pan/zoom/select done, needs node drag, connection validation |
| 05 | Execution Engine | 🔴 Not Started | Port NodeManager execution logic |
| 06 | Context Menu & Node Registry | 🔴 Not Started | Node discovery, right-click menu |
| 07 | Custom Node Editors | 🔴 Not Started | Socket value editors |
| 08 | Styling & Theming | 🟡 Partial | Base CSS done, needs theme model |
| 09 | Serialization & Persistence | 🔴 Not Started | JSON save/load |
| 10 | Integration & Testing | 🟡 Partial | MAUI host works, needs full testing |
| 11 | Performance Optimization | 🔴 Not Started | Viewport culling, virtualization |
| 12 | Documentation & Migration Guide | 🔴 Not Started | API docs, migration guide |
| 13 | Plugin System | 🟡 Partial | Platform guard exists, needs loader |
| 14 | Packaging, Deployment & Release | 🔴 Not Started | NuGet, CI/CD |
| 15 | Accessibility, Observability & Diagnostics | 🔴 Not Started | ARIA, logging |

## Completed Infrastructure

### NodeEditor.Blazor Library
- **Models**: `Point2D`, `Size2D`, `Rect2D`, `ColorValue`, `StrokeStyle`, `NodeData`, `SocketData`, `ConnectionData`, `SocketValue`
- **ViewModels**: `ViewModelBase`, `NodeViewModel`, `SocketViewModel` (with INotifyPropertyChanged)
- **Services**: `NodeEditorState` (event-based), `CoordinateConverter`, `SocketTypeResolver`, `PlatformGuard`, `NodeEditorServiceExtensions`
- **Components**: `NodeEditorCanvas`, `NodeComponent`, `SocketComponent`, `ConnectionPath`
- **Styles**: `wwwroot/css/node-editor.css` (dark theme, responsive)

### NodeEditorMax MAUI Host
- References NodeEditor.Blazor
- Services registered via `AddNodeEditor()`
- Sample graph with 5 nodes and 5 connections
- Runs on Windows (verified), iOS, Android, Mac Catalyst

## Stages
1. [Stage 01 — Project Setup & Core Infrastructure](Stage-01-Project-Setup.md)
2. [Stage 02 — Separate Visual from Logical Components](Stage-02-Separate-Visual-From-Logic.md)
3. [Stage 03 — Blazor Component Hierarchy](Stage-03-Blazor-Component-Hierarchy.md)
4. [Stage 04 — Interaction Logic](Stage-04-Interaction-Logic.md)
5. [Stage 05 — Execution Engine](Stage-05-Execution-Engine.md)
6. [Stage 06 — Context Menu & Node Registry](Stage-06-Context-Menu-Node-Registry.md)
7. [Stage 07 — Custom Node Editors](Stage-07-Custom-Node-Editors.md)
8. [Stage 08 — Styling & Theming](Stage-08-Styling-Theming.md)
9. [Stage 09 — Serialization & Persistence](Stage-09-Serialization.md)
10. [Stage 10 — Integration & Testing](Stage-10-Integration-Testing.md)
11. [Stage 11 — Performance Optimization](Stage-11-Performance-Optimization.md)
12. [Stage 12 — Documentation & Migration Guide](Stage-12-Documentation.md)
13. [Stage 13 — Plugin System (Desktop/Android Only)](Stage-13-Plugin-System.md)
14. [Stage 14 — Packaging, Deployment & Release](Stage-14-Packaging-Deployment.md)
15. [Stage 15 — Accessibility, Observability & Diagnostics](Stage-15-Accessibility-Observability.md)

## Cross-Cutting Concerns (Not Yet Addressed)

These items span multiple stages and should be designed holistically:

1. **Undo/Redo** - Command pattern for history (affects Stage 04, 05, 09)
2. **Clipboard** - Copy/paste nodes (affects Stage 04, 09)
3. **Multi-Selection Operations** - Batch move/delete (affects Stage 04)
4. **Error Handling** - Unified exceptions, user-facing messages (all stages)
5. **Localization** - i18n for node names, menus, errors (affects Stage 06, 12)
6. **Touch Gestures** - Pinch zoom, two-finger pan (affects Stage 04, 10)

## Key Files Reference

| Purpose | Location |
|---------|----------|
| State management | `NodeEditor.Blazor/Services/NodeEditorState.cs` |
| Event args | `NodeEditor.Blazor/Services/StateChangeEventArgs.cs` |
| Coordinate conversion | `NodeEditor.Blazor/Services/CoordinateConverter.cs` |
| DI registration | `NodeEditor.Blazor/Services/NodeEditorServiceExtensions.cs` |
| Canvas component | `NodeEditor.Blazor/Components/NodeEditorCanvas.razor` |
| Node component | `NodeEditor.Blazor/Components/NodeComponent.razor` |
| Socket component | `NodeEditor.Blazor/Components/SocketComponent.razor` |
| Connection path | `NodeEditor.Blazor/Components/ConnectionPath.razor` |
| CSS styles | `NodeEditor.Blazor/wwwroot/css/node-editor.css` |
| MAUI host page | `NodeEditorMax/Components/Pages/Home.razor` |
| MAUI DI setup | `NodeEditorMax/MauiProgram.cs` |

