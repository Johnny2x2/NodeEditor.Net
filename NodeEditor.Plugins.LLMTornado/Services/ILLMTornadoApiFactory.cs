using LlmTornado;
using LlmTornado.Code;

namespace NodeEditor.Plugins.LLMTornado.Services;

public interface ILLMTornadoApiFactory
{
    TornadoApi Create(
        string? providerOverride = null,
        string? apiKeyOverride = null,
        string? organizationOverride = null,
        string? baseUrlOverride = null,
        string? apiVersionOverride = null);

    LLmProviders ResolveProvider(string? providerName);
}