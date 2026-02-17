namespace NodeEditor.Plugins.LLMTornado.Services;

public sealed class LLMTornadoConfigResolver
{
    public LLMTornadoRuntimeConfig Resolve()
    {
        return new LLMTornadoRuntimeConfig
        {
            Provider = Read("LLMTORNADO_PROVIDER") ?? "OpenAi",
            ApiKey = Read("LLMTORNADO_API_KEY"),
            Organization = Read("LLMTORNADO_ORGANIZATION"),
            BaseUrl = Read("LLMTORNADO_BASE_URL"),
            ApiVersion = Read("LLMTORNADO_API_VERSION")
        };
    }

    private static string? Read(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}