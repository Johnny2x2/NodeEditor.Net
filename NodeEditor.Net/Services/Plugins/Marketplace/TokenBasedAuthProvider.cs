using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NodeEditor.Net.Services.Plugins.Marketplace;

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
            "NodeEditor", "auth-tokens.dat");

        if (!string.IsNullOrEmpty(_options.RemoteApiUrl))
        {
            _httpClient.BaseAddress = new Uri(_options.RemoteApiUrl);
        }

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

    private static byte[] ProtectData(byte[] data)
    {
        if (OperatingSystem.IsWindows())
        {
            return ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
        }

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
