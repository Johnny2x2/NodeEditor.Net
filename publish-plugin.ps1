#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes a plugin to the local marketplace repository.

.DESCRIPTION
    This script builds a plugin project and publishes it to the local 
    marketplace repository folder. Users can then install it via the 
    Plugin Manager UI in the application.

.PARAMETER PluginProject
    Path to the plugin .csproj file.

.PARAMETER RepositoryPath
    Path to the local plugin repository. Default: ./plugin-repository

.PARAMETER Configuration
    Build configuration (Debug or Release). Default: Release

.EXAMPLE
    ./publish-plugin.ps1 -PluginProject NodeEditor.Plugins.Sample/NodeEditor.Plugins.Sample.csproj
    
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$PluginProject,
    
    [string]$RepositoryPath = "./plugin-repository",
    
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Resolve paths
$PluginProject = Resolve-Path $PluginProject -ErrorAction Stop
$ProjectDir = Split-Path $PluginProject -Parent
$ProjectName = [System.IO.Path]::GetFileNameWithoutExtension($PluginProject)

Write-Host "Publishing plugin: $ProjectName" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

# Build the plugin
Write-Host "`nBuilding plugin..." -ForegroundColor Yellow
dotnet build $PluginProject -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Find the output directory
$OutputDir = Join-Path $ProjectDir "bin\$Configuration\net10.0"
if (-not (Test-Path $OutputDir)) {
    # Try without specific TFM
    $OutputDir = Get-ChildItem -Path (Join-Path $ProjectDir "bin\$Configuration") -Directory | Select-Object -First 1 -ExpandProperty FullName
}

if (-not (Test-Path $OutputDir)) {
    Write-Host "Could not find build output directory" -ForegroundColor Red
    exit 1
}

# Read plugin.json to get the plugin ID
$ManifestPath = Join-Path $ProjectDir "plugin.json"
if (-not (Test-Path $ManifestPath)) {
    Write-Host "plugin.json not found in project directory" -ForegroundColor Red
    exit 1
}

$Manifest = Get-Content $ManifestPath | ConvertFrom-Json
$PluginId = $Manifest.id
$PluginName = $Manifest.name

if (-not $PluginId) {
    Write-Host "plugin.json must contain an 'id' field" -ForegroundColor Red
    exit 1
}

# Create repository folder for this plugin
$RepositoryPath = [System.IO.Path]::GetFullPath($RepositoryPath)
$PluginRepoDir = Join-Path $RepositoryPath $ProjectName

Write-Host "`nPublishing to: $PluginRepoDir" -ForegroundColor Yellow

if (Test-Path $PluginRepoDir) {
    Remove-Item -Path $PluginRepoDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PluginRepoDir -Force | Out-Null

# Copy all DLLs
Copy-Item -Path (Join-Path $OutputDir "*.dll") -Destination $PluginRepoDir -Force

# Copy deps.json if exists
$DepsJson = Join-Path $OutputDir "*.deps.json"
if (Test-Path $DepsJson) {
    Copy-Item -Path $DepsJson -Destination $PluginRepoDir -Force
}

# Copy plugin.json manifest
Copy-Item -Path $ManifestPath -Destination $PluginRepoDir -Force

# Copy plugin-marketplace.json if exists (extended metadata)
$MarketplaceManifest = Join-Path $ProjectDir "plugin-marketplace.json"
if (Test-Path $MarketplaceManifest) {
    Copy-Item -Path $MarketplaceManifest -Destination $PluginRepoDir -Force
}

Write-Host "`n✓ Published '$PluginName' (v$($Manifest.version)) to local repository" -ForegroundColor Green
Write-Host "  Users can now install it via Plugin Manager in the app" -ForegroundColor Gray
