using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Plugins;

public interface INodeProvider
{
    IEnumerable<NodeDefinition> GetNodeDefinitions();
}
