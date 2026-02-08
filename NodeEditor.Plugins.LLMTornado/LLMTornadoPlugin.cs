using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Plugins.LLMTornado;

public sealed class LLMTornadoPlugin : INodePlugin
{
    public string Name => "LLM Tornado Plugin";
    public string Id => "com.nodeeditormax.llmtornado";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(LLMTornadoPlugin).Assembly);
    }
}
