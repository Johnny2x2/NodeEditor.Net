# Stage 05 — Execution Engine

## Goal
Port NodeManager execution logic into a service layer compatible with MAUI/Blazor.

## Deliverables
- NodeExecutionService
- Execution events and cancellation support

## Tasks
1. Move execution flow from NodeManager into NodeExecutionService.
2. Ensure cancellation token support remains intact.
3. Add events for start, node executed, finish.

## Acceptance Criteria
- Execution produces the same outputs as the WinForms version.
- Feedback events propagate correctly.

### Testing Parameters
- NUnit/xUnit deterministic execution tests match WinForms outputs for sample graphs.
- NUnit/xUnit cancellation test: execution stops within 100ms after token cancel.

## Dependencies
Stage 02.

## Risks / Notes
- Avoid using UI thread assumptions.
