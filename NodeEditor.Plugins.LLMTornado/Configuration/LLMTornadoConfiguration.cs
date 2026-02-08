using LlmTornado;
using LlmTornado.Code;

namespace NodeEditor.Plugins.LLMTornado.Configuration;

/// <summary>
/// Reads LLM provider configuration from environment variables.
/// API keys are never serialized or persisted in graphs.
/// </summary>
public sealed class LLMTornadoConfiguration
{
    public string? ApiKey { get; set; }
    public string Provider { get; set; } = "OpenAi";
    public string? Organization { get; set; }
    public string? BaseUrl { get; set; }
    public Dictionary<string, string> ProviderKeys { get; set; } = new();

    private static TornadoApi? _cachedApi;
    private static string? _cachedApiKey;

    /// <summary>
    /// Returns a cached <see cref="TornadoApi"/> instance, re-creating it only
    /// when the primary API key environment variable changes.
    /// </summary>
    public static TornadoApi GetOrCreateApi()
    {
        var currentKey = Environment.GetEnvironmentVariable("LLMTORNADO_API_KEY");
        if (_cachedApi is not null && currentKey == _cachedApiKey)
        {
            return _cachedApi;
        }

        var config = FromEnvironment();
        _cachedApi = config.CreateApi();
        _cachedApiKey = currentKey;
        return _cachedApi;
    }

    public static LLMTornadoConfiguration FromEnvironment()
    {
        var config = new LLMTornadoConfiguration
        {
            ApiKey = Environment.GetEnvironmentVariable("LLMTORNADO_API_KEY"),
            Provider = Environment.GetEnvironmentVariable("LLMTORNADO_PROVIDER") ?? "OpenAi",
            Organization = Environment.GetEnvironmentVariable("LLMTORNADO_ORGANIZATION"),
            BaseUrl = Environment.GetEnvironmentVariable("LLMTORNADO_BASE_URL"),
        };

        // Load provider-specific keys
        var providerNames = new[] { "OpenAi", "Anthropic", "Cohere", "Google" };
        foreach (var name in providerNames)
        {
            var key = Environment.GetEnvironmentVariable($"LLMTORNADO_{name.ToUpperInvariant()}_KEY");
            if (!string.IsNullOrEmpty(key))
            {
                config.ProviderKeys[name] = key;
            }
        }

        return config;
    }

    public TornadoApi CreateApi()
    {
        var authentications = new List<ProviderAuthentication>();

        // Map provider name to LLmProviders enum
        if (!string.IsNullOrEmpty(ApiKey))
        {
            var provider = ResolveProvider(Provider);
            authentications.Add(new ProviderAuthentication(provider, ApiKey));
        }

        // Add provider-specific keys
        foreach (var kvp in ProviderKeys)
        {
            var provider = ResolveProvider(kvp.Key);
            authentications.Add(new ProviderAuthentication(provider, kvp.Value));
        }

        if (authentications.Count == 0)
        {
            throw new InvalidOperationException(
                "No API key configured. Set the LLMTORNADO_API_KEY environment variable.");
        }

        return new TornadoApi(authentications);
    }

    private static LLmProviders ResolveProvider(string name) => name.ToLowerInvariant() switch
    {
        "openai" => LLmProviders.OpenAi,
        "anthropic" => LLmProviders.Anthropic,
        "cohere" => LLmProviders.Cohere,
        "google" => LLmProviders.Google,
        _ => LLmProviders.OpenAi
    };
}
