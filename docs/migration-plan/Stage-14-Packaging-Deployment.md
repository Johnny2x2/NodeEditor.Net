# Stage 14 — Packaging, Deployment & Release

## Status: 🔴 Not Started

### What's Done
- ✅ Blazor library builds successfully
- ✅ MAUI app builds for Windows, Android, iOS, Mac Catalyst

### What's Remaining
- ❌ NuGet package metadata in `.csproj`
- ❌ GitHub Actions CI workflow
- ❌ Automated testing in CI
- ❌ Package publishing workflow
- ❌ Release notes template
- ❌ CHANGELOG.md

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
- Unit tests pass in CI.
- Package validation ensures no WinForms/System.Drawing references.

## Implementation Notes (for next developer)

### NuGet Package Metadata
Add to `NodeEditor.Blazor/NodeEditor.Blazor.csproj`:
```xml
<PropertyGroup>
    <PackageId>NodeEditor.Blazor</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Company>Your Company</Company>
    <Description>A cross-platform node editor component for Blazor and MAUI</Description>
    <PackageTags>node-editor;blazor;maui;visual-scripting</PackageTags>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/Johnny2x2/NodeEditorMax</PackageProjectUrl>
    <RepositoryUrl>https://github.com/Johnny2x2/NodeEditorMax.git</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
</PropertyGroup>

<ItemGroup>
    <None Include="..\README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

### GitHub Actions Workflow
Create `.github/workflows/ci.yml`:
```yaml
name: CI

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      
      - name: Restore
        run: dotnet restore
      
      - name: Build
        run: dotnet build --no-restore --configuration Release
      
      - name: Test
        run: dotnet test --no-build --configuration Release --verbosity normal
      
      - name: Pack
        run: dotnet pack NodeEditor.Blazor/NodeEditor.Blazor.csproj --no-build --configuration Release --output ./artifacts
      
      - name: Upload artifacts
        uses: actions/upload-artifact@v4
        with:
          name: nuget-package
          path: ./artifacts/*.nupkg
```

### Release Workflow
Create `.github/workflows/release.yml` for tagged releases:
```yaml
name: Release

on:
  push:
    tags: ['v*']

jobs:
  publish:
    runs-on: windows-latest
    steps:
      # ... build steps ...
      
      - name: Publish to NuGet
        run: dotnet nuget push ./artifacts/*.nupkg --api-key ${{ secrets.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json
```

### CHANGELOG Format
```markdown
# Changelog

## [1.0.0] - 2026-XX-XX

### Added
- Initial release
- Cross-platform node editor components
- Event-based state management
- Dark theme with responsive design

### Migration
- See MIGRATION.md for WinForms migration guide
```

## Checklist
- [ ] NuGet package metadata complete
- [ ] CI pipeline produces artifacts
- [ ] Tests run in CI
- [ ] MAUI build verified for Windows
- [ ] MAUI build verified for Android
- [ ] Package published to NuGet (or private feed)
- [ ] CHANGELOG.md created
