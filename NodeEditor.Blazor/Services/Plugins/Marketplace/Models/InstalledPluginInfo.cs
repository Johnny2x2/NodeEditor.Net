namespace NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

/// <summary>
/// Information about an installed plugin.
/// </summary>
public sealed record InstalledPluginInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public DateTimeOffset InstalledAt { get; init; }

    public string? SourceId { get; init; }

    public required string InstallPath { get; init; }

    public bool IsEnabled { get; init; } = true;

    public bool IsLoaded { get; init; }

    public string? LoadError { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    public string? Category { get; init; }
}
