using LlmTornado;
using LlmTornado.Code;

namespace NodeEditor.Plugins.LLMTornado.Services;

public sealed class LLMTornadoApiFactory : ILLMTornadoApiFactory
{
    private const string UserAgent = "NodeEditor.Plugins.LLMTornado/2.0";
    private readonly LLMTornadoConfigResolver _resolver;

    public LLMTornadoApiFactory(LLMTornadoConfigResolver resolver)
    {
        _resolver = resolver;
    }

    public TornadoApi Create(
        string? providerOverride = null,
        string? apiKeyOverride = null,
        string? organizationOverride = null,
        string? baseUrlOverride = null,
        string? apiVersionOverride = null)
    {
        var config = _resolver.Resolve();

        var provider = ResolveProvider(FirstNonEmpty(providerOverride, config.Provider));
        var apiKey = FirstNonEmpty(apiKeyOverride, config.ApiKey);
        var organization = FirstNonEmpty(organizationOverride, config.Organization);
        var baseUrl = FirstNonEmpty(baseUrlOverride, config.BaseUrl);
        var apiVersion = FirstNonEmpty(apiVersionOverride, config.ApiVersion);

        TornadoApi api;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            api = new TornadoApi(provider);
        }
        else if (string.IsNullOrWhiteSpace(organization))
        {
            api = new TornadoApi(provider, apiKey);
        }
        else
        {
            api = new TornadoApi(provider, apiKey, organization);
        }

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            api.ApiUrlFormat = baseUrl;
        }

        if (!string.IsNullOrWhiteSpace(apiVersion))
        {
            api.ApiVersion = apiVersion;
        }

        api.RequestSettings.UserAgent = UserAgent;
        return api;
    }

    public LLmProviders ResolveProvider(string? providerName)
    {
        if (!string.IsNullOrWhiteSpace(providerName)
            && Enum.TryParse<LLmProviders>(providerName, true, out var provider)
            && provider != LLmProviders.Unknown)
        {
            return provider;
        }

        return LLmProviders.OpenAi;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}