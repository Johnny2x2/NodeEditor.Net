using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Plugins.OpenCv2;

public sealed class OpenCv2Plugin : INodePlugin
{
    public string Name => "OpenCV 2 Nodes";
    public string Id => "com.nodeeditormax.opencv2";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(OpenCv2Plugin).Assembly);
    }
}
