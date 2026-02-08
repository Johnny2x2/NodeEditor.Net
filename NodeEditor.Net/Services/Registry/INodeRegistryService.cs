using System.Reflection;
using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Registry;

public interface INodeRegistryService
{
    event EventHandler? RegistryChanged;

    IReadOnlyList<NodeDefinition> Definitions { get; }

    void EnsureInitialized(IEnumerable<Assembly>? assemblies = null);
    void RegisterFromAssembly(Assembly assembly);
    void RegisterPluginAssembly(Assembly assembly);
    void RegisterDefinitions(IEnumerable<NodeDefinition> definitions);
    int RemoveDefinitions(IEnumerable<NodeDefinition> definitions);
    int RemoveDefinitionsFromAssembly(Assembly assembly);
    NodeCatalog GetCatalog(string? search = null);
}
