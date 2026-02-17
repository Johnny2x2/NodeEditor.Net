using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Plugins;

public interface INodeProvider
{
    IEnumerable<NodeDefinition> GetNodeDefinitions();
}
