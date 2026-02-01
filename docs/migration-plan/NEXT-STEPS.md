# Next Steps (Stages 10–15)

This document aggregates the immediate next actions required to move the project toward a complete, cohesive release.

## Stage 10 — Integration & Testing
1. Add an error boundary around the editor surface in the MAUI host page.
2. Implement touch gestures in `NodeEditorCanvas` (pan, pinch zoom, tap select).
3. Run the manual test checklist on Windows + one mobile target and record results.
4. Verify iOS startup skips plugin loading cleanly and logs the skip.
5. Confirm save/load and execution flows in the MAUI host with sample graphs.

## Stage 11 — Performance Optimization
1. Implement viewport culling in `NodeEditorCanvas` using `State.Viewport` + `CoordinateConverter`.
2. Batch connection rendering into a single SVG path where possible.
3. Add `ShouldRender` overrides for `NodeComponent` and `ConnectionPath` with cached state.
4. Add a dev-only diagnostics overlay for FPS, visible counts, and memory.
5. Run a 30-minute soak test and capture memory profile results.

## Stage 12 — Documentation & Migration Guide
1. Write top-level README with install + minimal usage example.
2. Generate API reference for public types (services, models, components).
3. Create MIGRATION.md with WinForms → Blazor mapping and examples.
4. Add a working sample project and run instructions (MAUI + WebHost).
5. Write a custom node + custom editor tutorial using `INodeContext`.

## Stage 13 — Plugin System
1. Add a sample plugin project and document build/deploy steps.
2. Add tests for deterministic loading and idempotent registration.
3. Define a security policy (allowlist, signing, or checksum validation).
4. Document configuration (`PluginOptions`) and platform limitations.
5. Add structured logs for load failures and validation rejects.

## Stage 14 — Packaging, Deployment & Release
1. Add NuGet metadata to `NodeEditor.Blazor.csproj` (ID, version, license, repo, readme).
2. Enable SourceLink and symbol packages.
3. Add CI workflow to build, test, and pack on PRs.
4. Add release workflow for tagged versions and NuGet publish.
5. Create CHANGELOG.md and release notes template.

## Stage 15 — Accessibility, Observability & Diagnostics
1. Add ARIA roles/labels to node, socket, and canvas components.
2. Implement keyboard navigation and shortcuts for selection and movement.
3. Audit contrast and add a high-contrast theme option.
4. Respect reduced-motion preference for animations.
5. Add structured logging for execution and plugin load failures.
