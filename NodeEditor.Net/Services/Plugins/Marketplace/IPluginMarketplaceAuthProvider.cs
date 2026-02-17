namespace NodeEditor.Net.Services.Plugins.Marketplace;

/// <summary>
/// Abstraction for marketplace authentication.
/// Placeholder for future online marketplace integration.
/// </summary>
public interface IPluginMarketplaceAuthProvider
{
    bool IsAuthenticated { get; }

    MarketplaceUserInfo? CurrentUser { get; }

    Task<AuthResult> SignInAsync(string username, string password, CancellationToken cancellationToken = default);

    Task SignOutAsync(CancellationToken cancellationToken = default);

    Task<IDictionary<string, string>> GetAuthHeadersAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Basic user info from marketplace.
/// </summary>
public sealed record MarketplaceUserInfo(
    string UserId,
    string Username,
    string? Email,
    string? AvatarUrl);

/// <summary>
/// Authentication result.
/// </summary>
public sealed record AuthResult(
    bool Success,
    string? ErrorMessage = null,
    MarketplaceUserInfo? User = null);
