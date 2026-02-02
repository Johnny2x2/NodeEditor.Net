# Stage 3: Remote Marketplace Integration and Advanced Features

## Overview

This stage extends the marketplace system to support remote/online plugin repositories, adds caching for performance, implements authentication for publishing, and provides configuration UI for managing marketplace sources.

## Prerequisites

- Stage 1 completed (abstractions, models, local storage)
- Stage 2 completed (UI components and Plugin Manager dialog)
- Local marketplace functionality fully working

## Goals

- [x] Create HTTP-based marketplace source for online repositories
- [x] Implement caching layer for marketplace data
- [x] Add authentication provider for user accounts
- [x] Create settings UI for managing marketplace sources
- [x] Add plugin publishing workflow (optional, for plugin authors)
- [x] Implement offline support with cached data
- [x] Add telemetry/analytics hooks (optional)

---

## 1. Remote Marketplace Client

### 1.1 RemotePluginMarketplaceSource

HTTP client implementation for connecting to an online marketplace API.

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/RemotePluginMarketplaceSource.cs`

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

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
        // Build cache key
        var cacheKey = BuildCacheKey("search", query, category, tags);
        
        // Try cache first
        var cached = await _cache.GetAsync<List<MarketplacePluginInfo>>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Returning cached search results for {CacheKey}", cacheKey);
            return cached;
        }
        
        try
        {
            await AddAuthHeadersAsync(cancellationToken);
            
            // Build query string
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
            
            var endpoint = "plugins" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
            
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<MarketplaceSearchResponse>(
                _jsonOptions, cancellationToken);
            
            var plugins = result?.Plugins ?? [];
            
            // Set source ID
            plugins = plugins.Select(p => p with { SourceId = SourceId }).ToList();
            
            // Cache the result
            await _cache.SetAsync(cacheKey, plugins, 
                TimeSpan.FromMinutes(_options.CacheDurationMinutes), cancellationToken);
            
            return plugins;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to search remote marketplace");
            
            // Fall back to cache if available (stale data)
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
            
            // Save to temp file
            var tempPath = Path.Combine(Path.GetTempPath(), $"{pluginId}-{Guid.NewGuid():N}.zip");
            
            await using var fileStream = File.Create(tempPath);
            await using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await downloadStream.CopyToAsync(fileStream, cancellationToken);
            
            // Get version from headers if available
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
        // HttpClient is typically managed by the DI container
    }
    
    private sealed record MarketplaceSearchResponse(
        List<MarketplacePluginInfo> Plugins,
        int TotalCount,
        int Page,
        int PageSize);
}
```

---

## 2. Caching Layer

### 2.1 IPluginMarketplaceCache Interface

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/IPluginMarketplaceCache.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Cache for marketplace data to improve performance and provide offline support.
/// </summary>
public interface IPluginMarketplaceCache
{
    /// <summary>
    /// Get a cached value.
    /// </summary>
    /// <typeparam name="T">Type of the cached value.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="allowStale">Whether to return stale data if fresh data is unavailable.</param>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default, bool allowStale = false) 
        where T : class;
    
    /// <summary>
    /// Set a cached value.
    /// </summary>
    /// <typeparam name="T">Type of the value to cache.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">How long to cache the value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) 
        where T : class;
    
    /// <summary>
    /// Remove a cached value.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Clear all cached values.
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

### 2.2 FileBasedMarketplaceCache Implementation

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/FileBasedMarketplaceCache.cs`

```csharp
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// File-based cache implementation for marketplace data.
/// Stores cached data in JSON files for persistence across sessions.
/// </summary>
public sealed class FileBasedMarketplaceCache : IPluginMarketplaceCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<FileBasedMarketplaceCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };
    
    public FileBasedMarketplaceCache(
        IOptions<MarketplaceOptions> options,
        ILogger<FileBasedMarketplaceCache> logger)
    {
        _logger = logger;
        
        var basePath = options.Value.CacheDirectory ?? 
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "NodeEditorMax", "marketplace-cache");
        
        _cacheDirectory = basePath;
        Directory.CreateDirectory(_cacheDirectory);
    }
    
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default, bool allowStale = false) 
        where T : class
    {
        var filePath = GetFilePath(key);
        
        if (!File.Exists(filePath))
            return null;
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            var entry = JsonSerializer.Deserialize<CacheEntry<T>>(json, JsonOptions);
            
            if (entry is null)
                return null;
            
            // Check expiration
            if (entry.ExpiresAt < DateTimeOffset.UtcNow)
            {
                if (!allowStale)
                {
                    _logger.LogDebug("Cache entry expired for {Key}", key);
                    return null;
                }
                
                _logger.LogDebug("Returning stale cache entry for {Key}", key);
            }
            
            return entry.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read cache entry for {Key}", key);
            return null;
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default) 
        where T : class
    {
        var filePath = GetFilePath(key);
        
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var entry = new CacheEntry<T>
            {
                Value = value,
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(expiration)
            };
            
            var json = JsonSerializer.Serialize(entry, JsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            
            _logger.LogDebug("Cached {Key} with expiration {Expiration}", key, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write cache entry for {Key}", key);
        }
        finally
        {
            _lock.Release();
        }
    }
    
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        var filePath = GetFilePath(key);
        
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("Removed cache entry for {Key}", key);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove cache entry for {Key}", key);
        }
        
        return Task.CompletedTask;
    }
    
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(_cacheDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(_cacheDirectory, "*.json"))
                {
                    File.Delete(file);
                }
                _logger.LogInformation("Cleared marketplace cache");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear cache");
        }
        
        return Task.CompletedTask;
    }
    
    private string GetFilePath(string key)
    {
        // Create a safe filename from the key
        var safeKey = Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes(key))
            .Replace("/", "_")
            .Replace("+", "-");
        
        return Path.Combine(_cacheDirectory, $"{safeKey}.json");
    }
    
    private sealed class CacheEntry<T>
    {
        public required T Value { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
}
```

---

## 3. Authentication Provider

### 3.1 TokenBasedAuthProvider Implementation

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/TokenBasedAuthProvider.cs`

```csharp
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Token-based authentication provider for marketplace.
/// Stores tokens securely and handles refresh.
/// </summary>
public sealed class TokenBasedAuthProvider : IPluginMarketplaceAuthProvider
{
    private readonly HttpClient _httpClient;
    private readonly MarketplaceOptions _options;
    private readonly ILogger<TokenBasedAuthProvider> _logger;
    private readonly string _tokenStorePath;
    
    private AuthTokens? _tokens;
    private MarketplaceUserInfo? _currentUser;
    
    public bool IsAuthenticated => _tokens is not null && !IsTokenExpired(_tokens);
    public MarketplaceUserInfo? CurrentUser => _currentUser;
    
    public TokenBasedAuthProvider(
        HttpClient httpClient,
        IOptions<MarketplaceOptions> options,
        ILogger<TokenBasedAuthProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        
        _tokenStorePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NodeEditorMax", "auth-tokens.dat");
        
        // Try to load existing tokens
        _ = LoadTokensAsync();
    }
    
    public async Task<AuthResult> SignInAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_options.RemoteApiUrl))
        {
            return new AuthResult(false, "No marketplace API configured.");
        }
        
        try
        {
            _httpClient.BaseAddress = new Uri(_options.RemoteApiUrl);
            
            var response = await _httpClient.PostAsJsonAsync("auth/login", new
            {
                Username = username,
                Password = password
            }, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AuthResult(false, $"Authentication failed: {error}");
            }
            
            var result = await response.Content.ReadFromJsonAsync<AuthLoginResponse>(
                cancellationToken: cancellationToken);
            
            if (result is null)
            {
                return new AuthResult(false, "Invalid response from server.");
            }
            
            _tokens = new AuthTokens
            {
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn)
            };
            
            _currentUser = result.User;
            
            await SaveTokensAsync(cancellationToken);
            
            _logger.LogInformation("Successfully signed in as {Username}", username);
            
            return new AuthResult(true, User: _currentUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sign in failed");
            return new AuthResult(false, ex.Message);
        }
    }
    
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_tokens is not null && !string.IsNullOrEmpty(_options.RemoteApiUrl))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _tokens.AccessToken);
                
                await _httpClient.PostAsync("auth/logout", null, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify server of logout");
        }
        finally
        {
            _tokens = null;
            _currentUser = null;
            
            if (File.Exists(_tokenStorePath))
            {
                File.Delete(_tokenStorePath);
            }
            
            _logger.LogInformation("Signed out");
        }
    }
    
    public async Task<IDictionary<string, string>> GetAuthHeadersAsync(CancellationToken cancellationToken = default)
    {
        if (_tokens is null)
        {
            return new Dictionary<string, string>();
        }
        
        // Check if token needs refresh
        if (IsTokenExpired(_tokens) && !string.IsNullOrEmpty(_tokens.RefreshToken))
        {
            await RefreshTokenAsync(cancellationToken);
        }
        
        if (_tokens is null)
        {
            return new Dictionary<string, string>();
        }
        
        return new Dictionary<string, string>
        {
            ["Authorization"] = $"Bearer {_tokens.AccessToken}"
        };
    }
    
    private async Task RefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokens is null || string.IsNullOrEmpty(_tokens.RefreshToken))
            return;
        
        try
        {
            _logger.LogDebug("Refreshing access token");
            
            var response = await _httpClient.PostAsJsonAsync("auth/refresh", new
            {
                RefreshToken = _tokens.RefreshToken
            }, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Token refresh failed, signing out");
                await SignOutAsync(cancellationToken);
                return;
            }
            
            var result = await response.Content.ReadFromJsonAsync<AuthLoginResponse>(
                cancellationToken: cancellationToken);
            
            if (result is not null)
            {
                _tokens = new AuthTokens
                {
                    AccessToken = result.AccessToken,
                    RefreshToken = result.RefreshToken ?? _tokens.RefreshToken,
                    ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn)
                };
                
                await SaveTokensAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            await SignOutAsync(cancellationToken);
        }
    }
    
    private static bool IsTokenExpired(AuthTokens tokens)
    {
        // Consider expired 5 minutes before actual expiration
        return tokens.ExpiresAt < DateTimeOffset.UtcNow.AddMinutes(5);
    }
    
    private async Task SaveTokensAsync(CancellationToken cancellationToken)
    {
        if (_tokens is null) return;
        
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_tokenStorePath)!);
            
            var json = JsonSerializer.Serialize(_tokens);
            var encrypted = ProtectData(Encoding.UTF8.GetBytes(json));
            
            await File.WriteAllBytesAsync(_tokenStorePath, encrypted, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save auth tokens");
        }
    }
    
    private async Task LoadTokensAsync()
    {
        if (!File.Exists(_tokenStorePath))
            return;
        
        try
        {
            var encrypted = await File.ReadAllBytesAsync(_tokenStorePath);
            var decrypted = UnprotectData(encrypted);
            var json = Encoding.UTF8.GetString(decrypted);
            
            _tokens = JsonSerializer.Deserialize<AuthTokens>(json);
            
            if (_tokens is not null && !IsTokenExpired(_tokens))
            {
                _logger.LogDebug("Loaded existing auth tokens");
            }
            else
            {
                _tokens = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load auth tokens");
            _tokens = null;
        }
    }
    
    // Platform-specific data protection
    private static byte[] ProtectData(byte[] data)
    {
        // Use DPAPI on Windows, or a fallback for other platforms
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }
        
        // For non-Windows, use a simple approach (should use platform keychain in production)
        return data;
    }
    
    private static byte[] UnprotectData(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
        }
        
        return data;
    }
    
    private sealed class AuthTokens
    {
        public required string AccessToken { get; init; }
        public string? RefreshToken { get; init; }
        public DateTimeOffset ExpiresAt { get; init; }
    }
    
    private sealed record AuthLoginResponse(
        string AccessToken,
        string? RefreshToken,
        int ExpiresIn,
        MarketplaceUserInfo User);
}
```

---

## 4. Aggregated Marketplace Source

Combines multiple sources (local + remote) into a unified interface.

### 4.1 AggregatedPluginMarketplaceSource

**File:** `NodeEditor.Blazor/Services/Plugins/Marketplace/AggregatedPluginMarketplaceSource.cs`

```csharp
using Microsoft.Extensions.Logging;
using NodeEditor.Blazor.Services.Plugins.Marketplace.Models;

namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Aggregates multiple marketplace sources into a single unified source.
/// </summary>
public sealed class AggregatedPluginMarketplaceSource : IPluginMarketplaceSource
{
    private readonly IEnumerable<IPluginMarketplaceSource> _sources;
    private readonly ILogger<AggregatedPluginMarketplaceSource> _logger;
    
    public string SourceId => "aggregated";
    public string DisplayName => "All Sources";
    
    public AggregatedPluginMarketplaceSource(
        IEnumerable<IPluginMarketplaceSource> sources,
        ILogger<AggregatedPluginMarketplaceSource> logger)
    {
        // Exclude self to prevent infinite recursion
        _sources = sources.Where(s => s.SourceId != SourceId).ToList();
        _logger = logger;
    }
    
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var tasks = _sources.Select(s => s.IsAvailableAsync(cancellationToken));
        var results = await Task.WhenAll(tasks);
        return results.Any(r => r);
    }
    
    public async Task<IReadOnlyList<MarketplacePluginInfo>> SearchAsync(
        string? query = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var allPlugins = new Dictionary<string, MarketplacePluginInfo>();
        
        var tasks = _sources.Select(async source =>
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    return Enumerable.Empty<MarketplacePluginInfo>();
                
                return await source.SearchAsync(query, category, tags, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search source {SourceId}", source.SourceId);
                return Enumerable.Empty<MarketplacePluginInfo>();
            }
        });
        
        var results = await Task.WhenAll(tasks);
        
        foreach (var plugins in results)
        {
            foreach (var plugin in plugins)
            {
                // If plugin exists from multiple sources, keep the one with higher version
                if (allPlugins.TryGetValue(plugin.Id, out var existing))
                {
                    var existingVersion = Version.TryParse(existing.Version, out var ev) ? ev : new Version(0, 0, 0);
                    var newVersion = Version.TryParse(plugin.Version, out var nv) ? nv : new Version(0, 0, 0);
                    
                    if (newVersion > existingVersion)
                    {
                        allPlugins[plugin.Id] = plugin;
                    }
                }
                else
                {
                    allPlugins[plugin.Id] = plugin;
                }
            }
        }
        
        return allPlugins.Values
            .OrderBy(p => p.Name)
            .ToList();
    }
    
    public async Task<MarketplacePluginInfo?> GetDetailsAsync(
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        foreach (var source in _sources)
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    continue;
                
                var plugin = await source.GetDetailsAsync(pluginId, cancellationToken);
                if (plugin is not null)
                    return plugin;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get details from source {SourceId}", source.SourceId);
            }
        }
        
        return null;
    }
    
    public async Task<PluginDownloadResult> DownloadAsync(
        string pluginId,
        string? version = null,
        CancellationToken cancellationToken = default)
    {
        // First find which source has this plugin
        foreach (var source in _sources)
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    continue;
                
                var plugin = await source.GetDetailsAsync(pluginId, cancellationToken);
                if (plugin is not null)
                {
                    return await source.DownloadAsync(pluginId, version, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to download from source {SourceId}", source.SourceId);
            }
        }
        
        return new PluginDownloadResult(false, ErrorMessage: $"Plugin '{pluginId}' not found in any source.");
    }
    
    public async Task<IReadOnlyList<string>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var allCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        var tasks = _sources.Select(async source =>
        {
            try
            {
                if (!await source.IsAvailableAsync(cancellationToken))
                    return Enumerable.Empty<string>();
                
                return await source.GetCategoriesAsync(cancellationToken);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        });
        
        var results = await Task.WhenAll(tasks);
        
        foreach (var categories in results)
        {
            foreach (var category in categories)
            {
                allCategories.Add(category);
            }
        }
        
        return allCategories.OrderBy(c => c).ToList();
    }
}
```

---

## 5. Marketplace Settings Component

### 5.1 MarketplaceSettingsPanel.razor

**File:** `NodeEditor.Blazor/Components/Marketplace/MarketplaceSettingsPanel.razor`

```razor
@namespace NodeEditor.Blazor.Components.Marketplace
@using NodeEditor.Blazor.Services.Plugins.Marketplace
@using Microsoft.Extensions.Options
@inject IOptions<MarketplaceOptions> Options
@inject IPluginMarketplaceAuthProvider? AuthProvider
@inject IPluginMarketplaceCache Cache

<div class="ne-marketplace-settings">
    <h3>Marketplace Settings</h3>
    
    @* Authentication Section *@
    <div class="ne-settings-section">
        <h4>Account</h4>
        
        @if (AuthProvider is not null)
        {
            @if (AuthProvider.IsAuthenticated && AuthProvider.CurrentUser is not null)
            {
                <div class="ne-settings-user">
                    <div class="ne-settings-user-avatar">
                        @if (!string.IsNullOrEmpty(AuthProvider.CurrentUser.AvatarUrl))
                        {
                            <img src="@AuthProvider.CurrentUser.AvatarUrl" alt="Avatar" />
                        }
                        else
                        {
                            <span>@AuthProvider.CurrentUser.Username[0].ToString().ToUpper()</span>
                        }
                    </div>
                    <div class="ne-settings-user-info">
                        <span class="ne-settings-user-name">@AuthProvider.CurrentUser.Username</span>
                        @if (!string.IsNullOrEmpty(AuthProvider.CurrentUser.Email))
                        {
                            <span class="ne-settings-user-email">@AuthProvider.CurrentUser.Email</span>
                        }
                    </div>
                    <button class="ne-settings-btn ne-settings-btn--secondary" @onclick="SignOut">
                        Sign Out
                    </button>
                </div>
            }
            else
            {
                <div class="ne-settings-login">
                    <div class="ne-settings-form-group">
                        <label>Username</label>
                        <input type="text" @bind="_username" placeholder="Enter username" />
                    </div>
                    <div class="ne-settings-form-group">
                        <label>Password</label>
                        <input type="password" @bind="_password" placeholder="Enter password" />
                    </div>
                    @if (!string.IsNullOrEmpty(_authError))
                    {
                        <div class="ne-settings-error">@_authError</div>
                    }
                    <button class="ne-settings-btn ne-settings-btn--primary" 
                            disabled="@_isSigningIn"
                            @onclick="SignIn">
                        @if (_isSigningIn)
                        {
                            <span>Signing in...</span>
                        }
                        else
                        {
                            <span>Sign In</span>
                        }
                    </button>
                </div>
            }
        }
        else
        {
            <p class="ne-settings-note">Authentication not configured for this installation.</p>
        }
    </div>
    
    @* Sources Section *@
    <div class="ne-settings-section">
        <h4>Plugin Sources</h4>
        
        <div class="ne-settings-sources">
            <div class="ne-settings-source">
                <div class="ne-settings-source-info">
                    <span class="ne-settings-source-name">Local Repository</span>
                    <span class="ne-settings-source-path">@Options.Value.LocalRepositoryPath</span>
                </div>
                <span class="ne-settings-source-status ne-settings-source-status--active">Active</span>
            </div>
            
            @if (!string.IsNullOrEmpty(Options.Value.RemoteApiUrl))
            {
                <div class="ne-settings-source">
                    <div class="ne-settings-source-info">
                        <span class="ne-settings-source-name">Online Marketplace</span>
                        <span class="ne-settings-source-path">@Options.Value.RemoteApiUrl</span>
                    </div>
                    <span class="ne-settings-source-status @(_remoteAvailable ? "ne-settings-source-status--active" : "ne-settings-source-status--inactive")">
                        @(_remoteAvailable ? "Connected" : "Unavailable")
                    </span>
                </div>
            }
        </div>
    </div>
    
    @* Cache Section *@
    <div class="ne-settings-section">
        <h4>Cache</h4>
        
        <div class="ne-settings-cache">
            <p>Cache duration: @Options.Value.CacheDurationMinutes minutes</p>
            <button class="ne-settings-btn ne-settings-btn--secondary" @onclick="ClearCache">
                Clear Cache
            </button>
        </div>
    </div>
    
    @* Options Section *@
    <div class="ne-settings-section">
        <h4>Options</h4>
        
        <div class="ne-settings-options">
            <label class="ne-settings-checkbox">
                <input type="checkbox" checked="@Options.Value.CheckForUpdatesOnStartup" 
                       @onchange="ToggleUpdateCheck" />
                <span>Check for plugin updates on startup</span>
            </label>
            
            <label class="ne-settings-checkbox">
                <input type="checkbox" checked="@Options.Value.PreferOnlineMarketplace"
                       @onchange="TogglePreferOnline" />
                <span>Prefer online marketplace over local</span>
            </label>
        </div>
    </div>
</div>

@code {
    private string _username = "";
    private string _password = "";
    private string _authError = "";
    private bool _isSigningIn;
    private bool _remoteAvailable;
    
    protected override async Task OnInitializedAsync()
    {
        // Check remote availability
        // This would need to be injected properly in real implementation
        _remoteAvailable = !string.IsNullOrEmpty(Options.Value.RemoteApiUrl);
    }
    
    private async Task SignIn()
    {
        if (AuthProvider is null) return;
        
        _isSigningIn = true;
        _authError = "";
        StateHasChanged();
        
        try
        {
            var result = await AuthProvider.SignInAsync(_username, _password);
            
            if (!result.Success)
            {
                _authError = result.ErrorMessage ?? "Sign in failed";
            }
            else
            {
                _username = "";
                _password = "";
            }
        }
        finally
        {
            _isSigningIn = false;
        }
    }
    
    private async Task SignOut()
    {
        if (AuthProvider is null) return;
        await AuthProvider.SignOutAsync();
    }
    
    private async Task ClearCache()
    {
        await Cache.ClearAsync();
    }
    
    private void ToggleUpdateCheck(ChangeEventArgs e)
    {
        // Would need to implement options persistence
    }
    
    private void TogglePreferOnline(ChangeEventArgs e)
    {
        // Would need to implement options persistence
    }
}
```

---

## 6. Updated MarketplaceOptions

**Update:** `NodeEditor.Blazor/Services/Plugins/Marketplace/MarketplaceOptions.cs`

```csharp
namespace NodeEditor.Blazor.Services.Plugins.Marketplace;

/// <summary>
/// Configuration options for the plugin marketplace.
/// </summary>
public sealed class MarketplaceOptions
{
    /// <summary>
    /// Path to the local plugin repository folder.
    /// </summary>
    public string LocalRepositoryPath { get; set; } = "plugin-repository";
    
    /// <summary>
    /// Manifest file name to look for in plugins.
    /// </summary>
    public string ManifestFileName { get; set; } = "plugin.json";
    
    /// <summary>
    /// URL of the online marketplace API.
    /// </summary>
    public string? RemoteApiUrl { get; set; }
    
    /// <summary>
    /// Whether to prefer online marketplace over local.
    /// </summary>
    public bool PreferOnlineMarketplace { get; set; } = false;
    
    /// <summary>
    /// Cache duration for marketplace queries (in minutes).
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 30;
    
    /// <summary>
    /// Whether to automatically check for updates on startup.
    /// </summary>
    public bool CheckForUpdatesOnStartup { get; set; } = false;
    
    /// <summary>
    /// Path to the cache directory.
    /// </summary>
    public string? CacheDirectory { get; set; }
    
    /// <summary>
    /// List of enabled marketplace sources.
    /// </summary>
    public List<string> EnabledSources { get; set; } = ["local", "remote"];
    
    /// <summary>
    /// Maximum number of plugins to display per page in search results.
    /// </summary>
    public int PageSize { get; set; } = 20;
    
    /// <summary>
    /// Whether to allow installing plugins from untrusted sources.
    /// </summary>
    public bool AllowUntrustedPlugins { get; set; } = false;
}
```

---

## 7. Service Registration Updates

**Update:** `NodeEditor.Blazor/Services/ServiceCollectionExtensions.cs`

```csharp
using NodeEditor.Blazor.Services.Plugins.Marketplace;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNodeEditor(this IServiceCollection services)
    {
        // ... existing registrations ...
        
        // Marketplace services
        services.AddOptions<MarketplaceOptions>();
        
        // Cache
        services.AddSingleton<IPluginMarketplaceCache, FileBasedMarketplaceCache>();
        
        // Auth provider
        services.AddSingleton<IPluginMarketplaceAuthProvider, TokenBasedAuthProvider>();
        
        // Marketplace sources
        services.AddSingleton<LocalPluginMarketplaceSource>();
        services.AddSingleton<IPluginMarketplaceSource>(sp => sp.GetRequiredService<LocalPluginMarketplaceSource>());
        
        // Remote source (only if configured)
        services.AddHttpClient<RemotePluginMarketplaceSource>();
        services.AddSingleton<IPluginMarketplaceSource>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MarketplaceOptions>>().Value;
            if (!string.IsNullOrEmpty(options.RemoteApiUrl))
            {
                return sp.GetRequiredService<RemotePluginMarketplaceSource>();
            }
            // Return a null object or skip
            return null!;
        });
        
        // Aggregated source for unified access
        services.AddSingleton<AggregatedPluginMarketplaceSource>();
        
        // Installation service
        services.AddSingleton<IPluginInstallationService, PluginInstallationService>();
        
        return services;
    }
}
```

---

## 8. Marketplace API Contract

For future server implementation, here's the expected API contract:

### 8.1 API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/health` | Health check |
| GET | `/plugins` | Search/list plugins |
| GET | `/plugins/{id}` | Get plugin details |
| GET | `/plugins/{id}/download` | Download plugin package |
| GET | `/categories` | List all categories |
| POST | `/auth/login` | User login |
| POST | `/auth/logout` | User logout |
| POST | `/auth/refresh` | Refresh access token |
| POST | `/plugins` | Publish new plugin (authenticated) |
| PUT | `/plugins/{id}` | Update plugin (authenticated, owner) |
| DELETE | `/plugins/{id}` | Delete plugin (authenticated, owner) |

### 8.2 Example API Responses

**GET /plugins?q=math**
```json
{
  "plugins": [
    {
      "id": "com.example.mathpack",
      "name": "Math Pack",
      "version": "2.0.0",
      "minApiVersion": "1.0.0",
      "author": "Example Corp",
      "description": "Advanced math operations",
      "category": "Math",
      "tags": ["math", "calculation", "arithmetic"],
      "iconUrl": "https://cdn.example.com/icons/mathpack.png",
      "downloadCount": 1500,
      "rating": 4.5,
      "ratingCount": 42,
      "lastUpdatedAt": "2026-01-15T10:30:00Z"
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 20
}
```

**POST /auth/login**
```json
// Request
{
  "username": "user@example.com",
  "password": "secret"
}

// Response
{
  "accessToken": "eyJ...",
  "refreshToken": "xyz...",
  "expiresIn": 3600,
  "user": {
    "userId": "u123",
    "username": "user",
    "email": "user@example.com",
    "avatarUrl": "https://example.com/avatars/u123.jpg"
  }
}
```

---

## 9. Deliverables Checklist

| File | Status |
|------|--------|
| `Services/Plugins/Marketplace/RemotePluginMarketplaceSource.cs` | To create |
| `Services/Plugins/Marketplace/IPluginMarketplaceCache.cs` | To create |
| `Services/Plugins/Marketplace/FileBasedMarketplaceCache.cs` | To create |
| `Services/Plugins/Marketplace/TokenBasedAuthProvider.cs` | To create |
| `Services/Plugins/Marketplace/AggregatedPluginMarketplaceSource.cs` | To create |
| `Components/Marketplace/MarketplaceSettingsPanel.razor` | To create |
| `Services/Plugins/Marketplace/MarketplaceOptions.cs` | To update |
| `Services/ServiceCollectionExtensions.cs` | To update |

---

## 10. Testing Strategy

### Unit Tests

1. **RemotePluginMarketplaceSourceTests.cs**
   - Mock HTTP responses
   - Test search with various filters
   - Test caching behavior
   - Test error handling and fallback to cache

2. **FileBasedMarketplaceCacheTests.cs**
   - Test get/set/remove operations
   - Test expiration logic
   - Test stale data retrieval

3. **TokenBasedAuthProviderTests.cs**
   - Test login/logout flow
   - Test token refresh
   - Test header generation

### Integration Tests

1. Create a mock HTTP server
2. Test full flow from UI to API
3. Verify caching works correctly across sessions

---

## 11. Configuration Example

**appsettings.json**
```json
{
  "Marketplace": {
    "LocalRepositoryPath": "plugin-repository",
    "RemoteApiUrl": "https://marketplace.nodeeditormax.com/api/v1",
    "CacheDurationMinutes": 30,
    "CheckForUpdatesOnStartup": true,
    "PreferOnlineMarketplace": true,
    "AllowUntrustedPlugins": false
  }
}
```

---

## 12. Migration Path from Local to Online

To migrate from local-only to online marketplace:

1. **Phase 1 - Local Only (Current)**
   - Use `LocalPluginMarketplaceSource`
   - Plugins distributed as zip files

2. **Phase 2 - Hybrid**
   - Add `RemotePluginMarketplaceSource`
   - Configure `RemoteApiUrl`
   - `AggregatedPluginMarketplaceSource` combines both

3. **Phase 3 - Online Primary**
   - Set `PreferOnlineMarketplace: true`
   - Local repository for development/testing only

4. **Phase 4 - Online Only (Optional)**
   - Disable local source in configuration
   - Full online marketplace experience

---

## Summary

This three-stage plan provides a complete path from a local-only plugin system to a full-featured online marketplace with:

- **Stage 1:** Core abstractions and local storage backend
- **Stage 2:** UI components for plugin management
- **Stage 3:** Remote marketplace, caching, and authentication

The architecture is designed for easy migration - simply configure the `RemoteApiUrl` when your online marketplace is ready, and the existing code will seamlessly integrate with it.
