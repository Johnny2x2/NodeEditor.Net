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
        var safeKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(key))
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
