using System.Security.Cryptography;

namespace NodeEditor.Net.Services.Mcp;

/// <summary>
/// Generates, persists, and validates MCP API keys.
/// Keys are stored at <c>%LocalAppData%/NodeEditor/mcp-api-key.dat</c>.
/// Thread-safe for concurrent validation from MCP request middleware.
/// </summary>
public sealed class McpApiKeyService
{
    private static readonly string DefaultStorageDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NodeEditor");

    private readonly string _storageDir;
    private readonly string _keyFilePath;
    private readonly string _enabledFilePath;
    private readonly object _lock = new();
    private string? _cachedKey;
    private bool? _cachedEnabled;

    /// <summary>
    /// Creates a service using the default storage directory (<c>%LocalAppData%/NodeEditor</c>).
    /// </summary>
    public McpApiKeyService() : this(DefaultStorageDir) { }

    /// <summary>
    /// Creates a service using a custom storage directory. Useful for test isolation.
    /// </summary>
    public McpApiKeyService(string storageDirectory)
    {
        _storageDir = storageDirectory;
        _keyFilePath = Path.Combine(_storageDir, "mcp-api-key.dat");
        _enabledFilePath = Path.Combine(_storageDir, "mcp-enabled.dat");
    }

    /// <summary>
    /// Returns the current API key, loading from disk if needed.
    /// Returns <c>null</c> if no key has been generated yet.
    /// </summary>
    public string? GetCurrentKey()
    {
        lock (_lock)
        {
            if (_cachedKey is not null)
                return _cachedKey;

            if (File.Exists(_keyFilePath))
            {
                _cachedKey = File.ReadAllText(_keyFilePath).Trim();
                return _cachedKey;
            }

            return null;
        }
    }

    /// <summary>
    /// Generates a new cryptographically random API key, persists it, and returns it.
    /// </summary>
    public string GenerateNewKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var key = Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        lock (_lock)
        {
            Directory.CreateDirectory(_storageDir);
            File.WriteAllText(_keyFilePath, key);
            _cachedKey = key;
        }

        return key;
    }

    /// <summary>
    /// Returns <c>true</c> if the given key matches the stored key.
    /// Uses fixed-time comparison to avoid timing attacks.
    /// </summary>
    public bool ValidateKey(string? candidateKey)
    {
        if (string.IsNullOrWhiteSpace(candidateKey))
            return false;

        var storedKey = GetCurrentKey();
        if (storedKey is null)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(candidateKey),
            System.Text.Encoding.UTF8.GetBytes(storedKey));
    }

    /// <summary>
    /// Deletes the stored key and clears the cache.
    /// </summary>
    public void RevokeKey()
    {
        lock (_lock)
        {
            _cachedKey = null;
            if (File.Exists(_keyFilePath))
                File.Delete(_keyFilePath);
        }
    }

    /// <summary>
    /// Returns whether the MCP server is enabled. Defaults to <c>false</c>.
    /// </summary>
    public bool IsEnabled()
    {
        lock (_lock)
        {
            if (_cachedEnabled.HasValue)
                return _cachedEnabled.Value;

            if (File.Exists(_enabledFilePath))
            {
                var text = File.ReadAllText(_enabledFilePath).Trim();
                _cachedEnabled = string.Equals(text, "true", StringComparison.OrdinalIgnoreCase);
                return _cachedEnabled.Value;
            }

            _cachedEnabled = false;
            return false;
        }
    }

    /// <summary>
    /// Persists the enabled/disabled state to disk.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(_storageDir);
            File.WriteAllText(_enabledFilePath, enabled ? "true" : "false");
            _cachedEnabled = enabled;
        }
    }
}
