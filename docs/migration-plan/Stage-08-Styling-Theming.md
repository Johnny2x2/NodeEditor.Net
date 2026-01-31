# Stage 08 — Styling & Theming

## Goal
Match or improve the visual style using CSS and theming models.

## Deliverables
- NodeEditorTheme model
- CSS for canvas, nodes, sockets, connections

## Tasks
1. Implement base CSS in wwwroot/css.
2. Add theme model and apply values via CSS variables.
3. Add type-based colors for sockets/connections.

## Acceptance Criteria
- Visual quality matches WinForms version.
- Theme can be swapped at runtime.

### Testing Parameters
- NUnit/xUnit visual regression checks for default and high-contrast themes.
- NUnit/xUnit theme swap test completes without layout reflow errors.

## Dependencies
Stage 03.

## Risks / Notes
- Ensure accessibility contrast for key elements.
