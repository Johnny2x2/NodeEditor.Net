# Stage 12 — Documentation & Migration Guide

## Goal
Provide clear guidance for users migrating from WinForms to MAUI Blazor.

## Deliverables
- Updated README for new library
- Migration guide with before/after examples

## Tasks
1. Document component API usage.
2. Provide examples for custom nodes and editors.
3. Outline breaking changes and replacements.
4. Document plugin support and iOS restriction.

## Acceptance Criteria
- Developers can embed the editor without additional support.
- Migration steps are unambiguous and complete.
- Plugin guidance clearly states iOS limitation.

### Testing Parameters
- NUnit/xUnit documentation walkthrough results in a running sample without manual fixes.
- NUnit/xUnit API reference checks match current public parameters and events.
- NUnit/xUnit doc validation ensures plugin guidance includes iOS restriction.

## Dependencies
Stage 10.

## Risks / Notes
- Keep API documentation consistent with actual component parameters.
