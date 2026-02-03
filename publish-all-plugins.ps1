#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes all plugins in the solution to the local marketplace repository.

.DESCRIPTION
    Development helper script that publishes all plugin projects to the 
    local repository so they can be installed via the Plugin Manager.

.EXAMPLE
    ./publish-all-plugins.ps1
#>

param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Find all plugin projects (projects with plugin.json)
$PluginProjects = @(
    "NodeEditor.Plugins.Sample\NodeEditor.Plugins.Sample.csproj",
    "NodeEditor.Plugins.LlmTornado\NodeEditor.Plugins.LlmTornado.csproj",
    "NodeEditor.Plugins.OpenCv2\NodeEditor.Plugins.OpenCv2.csproj"
)

Write-Host "Publishing all plugins to local repository..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host ""

foreach ($project in $PluginProjects) {
    $projectPath = Join-Path $ScriptDir $project
    if (Test-Path $projectPath) {
        & "$ScriptDir\publish-plugin.ps1" -PluginProject $projectPath -Configuration $Configuration
        Write-Host ""
    } else {
        Write-Host "Skipping $project (not found)" -ForegroundColor Yellow
    }
}

Write-Host "Done! All plugins published to ./plugin-repository" -ForegroundColor Green
Write-Host "Run the WebHost app and use Plugin Manager to install them." -ForegroundColor Gray
