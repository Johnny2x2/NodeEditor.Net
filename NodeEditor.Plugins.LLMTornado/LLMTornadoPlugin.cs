using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado;

public sealed class LLMTornadoPlugin : INodePlugin
{
    public string Name => "LLMTornado Plugin";
    public string Id => "com.nodeeditormax.llmtornado";
    public Version Version => new(2, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(LLMTornadoPlugin).Assembly);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<LLMTornadoConfigResolver>();
        services.AddSingleton<ILLMTornadoApiFactory, LLMTornadoApiFactory>();
    }
}