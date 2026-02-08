using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Remote HTTP-based plugin marketplace source.
/// Connects to an online marketplace API.
/// </summary>
public sealed class RemotePluginMarketplaceSource : IPluginMarketplaceSource, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly MarketplaceOptions _options;
    private readonly IPluginMarketplaceAuthProvider? _authProvider;
    private readonly IPluginMarketplaceCache _cache;
    private readonly ILogger<RemotePluginMarketplaceSource> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public string SourceId => "remote";
    public string DisplayName => "Online Marketplace";

    public RemotePluginMarketplaceSource(
        HttpClient httpClient,
        IOptions<MarketplaceOptions> options,
        IPluginMarketplaceCache cache,
        ILogger<RemotePluginMarketplaceSource> logger,
        IPluginMarketplaceAuthProvider? authProvider = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
        _authProvider = authProvider;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        if (!string.IsNullOrEmpty(_options.RemoteApiUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.RemoteApiUrl);
        }

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "NodeEditorMax-Marketplace/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.RemoteApiUrl))
            return false;

        try
        {
            var response = await _httpClient.GetAsync("health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote marketplace is not available");
            return false;
        }
    }

    public async Task<IReadOnlyList<MarketplacePluginInfo>> SearchAsync(
        string? query = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = BuildCacheKey("search", query, category, tags);

        var cached = await _cache.GetAsync<List<MarketplacePluginInfo>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Returning cached search results for {CacheKey}", cacheKey);
            return cached;
        }

        try
        {
            await AddAuthHeadersAsync(cancellationToken);

            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(query))
                queryParams.Add($"q={Uri.EscapeDataString(query)}");
            if (!string.IsNullOrWhiteSpace(category))
                queryParams.Add($"category={Uri.EscapeDataString(category)}");
            if (tags is not null)
            {
                foreach (var tag in tags)
                    queryParams.Add($"tag={Uri.EscapeDataString(tag)}");
            }

            var endpoint = "plugins" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : string.Empty);

            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<MarketplaceSearchResponse>(
                _jsonOptions, cancellationToken);

            var plugins = result?.Plugins ?? [];
            plugins = plugins.Select(p => p with { SourceId = SourceId }).ToList();

            await _cache.SetAsync(cacheKey, plugins,
                TimeSpan.FromMinutes(_options.CacheDurationMinutes), cancellationToken);

            return plugins;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to search remote marketplace");

            var stale = await _cache.GetAsync<List<MarketplacePluginInfo>>(cacheKey, cancellationToken, allowStale: true);
            if (stale is not null)
            {
                _logger.LogWarning("Returning stale cached data due to network error");
                return stale;
            }

            throw;
        }
    }

    public async Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"plugin:{pluginId}";

        var cached = await _cache.GetAsync<MarketplacePluginInfo>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        try
        {
            await AddAuthHeadersAsync(cancellationToken);

            var response = await _httpClient.GetAsync($"plugins/{pluginId}", cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            var plugin = await response.Content.ReadFromJsonAsync<MarketplacePluginInfo>(
                _jsonOptions, cancellationToken);

            if (plugin is not null)
            {
                plugin = plugin with { SourceId = SourceId };
                await _cache.SetAsync(cacheKey, plugin,
                    TimeSpan.FromMinutes(_options.CacheDurationMinutes), cancellationToken);
            }

            return plugin;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get plugin details for {PluginId}", pluginId);

            var stale = await _cache.GetAsync<MarketplacePluginInfo>(cacheKey, cancellationToken, allowStale: true);
            return stale;
        }
    }

    public async Task<PluginDownloadResult> DownloadAsync(
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await AddAuthHeadersAsync(cancellationToken);

            var endpoint = $"plugins/{pluginId}/download";
            if (!string.IsNullOrEmpty(version))
                endpoint += $"?version={Uri.EscapeDataString(version)}";

            var response = await _httpClient.GetAsync(endpoint,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new PluginDownloadResult(false, ErrorMessage: $"Plugin '{pluginId}' not found.");
            }

            response.EnsureSuccessStatusCode();

            var tempPath = Path.Combine(Path.GetTempPath(), $"{pluginId}-{Guid.NewGuid():N}.zip");

            await using var fileStream = File.Create(tempPath);
            await using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await downloadStream.CopyToAsync(fileStream, cancellationToken);

            var actualVersion = response.Headers.TryGetValues("X-Plugin-Version", out var versionHeaders)
                ? versionHeaders.FirstOrDefault() ?? version
                : version;

            return new PluginDownloadResult(
                Success: true,
                LocalPath: tempPath,
                Version: actualVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download plugin {PluginId}", pluginId);
            return new PluginDownloadResult(false, ErrorMessage: ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "categories";

        var cached = await _cache.GetAsync<List<string>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        try
        {
            await AddAuthHeadersAsync(cancellationToken);

            var response = await _httpClient.GetAsync("categories", cancellationToken);
            response.EnsureSuccessStatusCode();

            var categories = await response.Content.ReadFromJsonAsync<List<string>>(
                _jsonOptions, cancellationToken) ?? [];

            await _cache.SetAsync(cacheKey, categories,
                TimeSpan.FromHours(1), cancellationToken);

            return categories;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to get categories");
            return await _cache.GetAsync<List<string>>(cacheKey, cancellationToken, allowStale: true) ?? [];
        }
    }

    private async Task AddAuthHeadersAsync(CancellationToken cancellationToken)
    {
        if (_authProvider is null || !_authProvider.IsAuthenticated)
            return;

        var headers = await _authProvider.GetAuthHeadersAsync(cancellationToken);
        foreach (var (key, value) in headers)
        {
            _httpClient.DefaultRequestHeaders.Remove(key);
            _httpClient.DefaultRequestHeaders.Add(key, value);
        }
    }

    private static string BuildCacheKey(string prefix, string? query, string? category, IEnumerable<string>? tags)
    {
        var parts = new List<string> { prefix };
        if (!string.IsNullOrWhiteSpace(query)) parts.Add($"q={query}");
        if (!string.IsNullOrWhiteSpace(category)) parts.Add($"cat={category}");
        if (tags is not null) parts.AddRange(tags.Select(t => $"tag={t}"));
        return string.Join(":", parts);
    }

    public void Dispose()
    {
    }

    private sealed record MarketplaceSearchResponse(
        List<MarketplacePluginInfo> Plugins,
        int TotalCount,
        int Page,
        int PageSize);
}
