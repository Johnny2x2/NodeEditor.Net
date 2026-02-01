using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Plugins.LlmTornado;

public sealed class LlmTornadoPlugin : INodePlugin
{
    public string Name => "LLM Tornado Nodes";
    public string Id => "com.nodeeditormax.llmtornado";
    public Version Version => new(0, 1, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(LlmTornadoPlugin).Assembly);
    }
}
