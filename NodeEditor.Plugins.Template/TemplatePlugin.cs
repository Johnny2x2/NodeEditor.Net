using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Plugins.Template;

public sealed class TemplatePlugin : INodePlugin
{
    public string Name => "Template Plugin";
    public string Id => "com.nodeeditormax.template";
    public Version Version => new(0, 1, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(TemplatePlugin).Assembly);
    }
}

public sealed class TemplatePluginContext : INodeContext
{
    [Node("Echo", category: "SDK", description: "Pass input to output", isCallable: false)]
    public void Echo(string Input, out string Output)
    {
        Output = Input;
    }
}
