# Stage 14 — Packaging, Deployment & Release

## Goal
Prepare the library and MAUI host app for release with CI packaging, versioning, and platform distribution.

## Deliverables
- NuGet package for NodeEditor.Blazor
- Versioning strategy (SemVer)
- CI pipeline (build, test, package)
- Release checklist

## Tasks
1. Add package metadata to NodeEditor.Blazor.csproj.
2. Create CI workflow to run tests and build packages.
3. Generate release notes and changelog.
4. Validate MAUI app packaging for Windows and at least one mobile target.

## Acceptance Criteria
- Package builds successfully with proper metadata.
- CI tests pass on push and PRs.
- Release artifacts are produced consistently.

### Testing Parameters
- NUnit/xUnit unit tests pass in CI.
- Package validation ensures no WinForms/System.Drawing references.

## Detailed Notes
- Use deterministic builds and include source link if possible.
- Consider signing packages if distributing publicly.

## Checklist
- [ ] NuGet package metadata complete
- [ ] CI pipeline produces artifacts
- [ ] MAUI build verified for at least two platforms
