namespace NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

/// <summary>
/// Complete plugin information from the marketplace.
/// Extends the basic PluginManifest with marketplace-specific metadata.
/// </summary>
public sealed record MarketplacePluginInfo
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Version { get; init; }

    public required string MinApiVersion { get; init; }

    public string? Author { get; init; }

    public string? Description { get; init; }

    public string? LongDescription { get; init; }

    public string? Category { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();

    public string? IconUrl { get; init; }

    public string? HomepageUrl { get; init; }

    public string? RepositoryUrl { get; init; }

    public string? License { get; init; }

    public IReadOnlyList<PluginVersionInfo> AvailableVersions { get; init; } = Array.Empty<PluginVersionInfo>();

    public IReadOnlyList<string> Screenshots { get; init; } = Array.Empty<string>();

    public int DownloadCount { get; init; }

    public double? Rating { get; init; }

    public int RatingCount { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    public DateTimeOffset? LastUpdatedAt { get; init; }

    public string? SourceId { get; init; }

    public long? PackageSizeBytes { get; init; }
}

/// <summary>
/// Information about a specific plugin version.
/// </summary>
public sealed record PluginVersionInfo(
    string Version,
    string MinApiVersion,
    DateTimeOffset? ReleasedAt,
    string? ReleaseNotes,
    long? PackageSizeBytes);
