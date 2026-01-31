using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Plugins;

public interface INodePlugin
{
    string Name { get; }
    string Id { get; }
    Version Version { get; }
    Version MinApiVersion { get; }

    void Register(NodeRegistryService registry);

    void Unload() { }
}
