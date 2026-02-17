namespace NodeEditor.Plugins.LLMTornado.Services;

public sealed class LLMTornadoRuntimeConfig
{
    public string Provider { get; init; } = "OpenAi";
    public string? ApiKey { get; init; }
    public string? Organization { get; init; }
    public string? BaseUrl { get; init; }
    public string? ApiVersion { get; init; }
}