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
