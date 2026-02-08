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
    Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default,
        bool allowStale = false)
        where T : class;

    /// <summary>
    /// Set a cached value.
    /// </summary>
    /// <typeparam name="T">Type of the value to cache.</typeparam>
    /// <param name="key">Cache key.</param>
    /// <param name="value">Value to cache.</param>
    /// <param name="expiration">How long to cache the value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
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
