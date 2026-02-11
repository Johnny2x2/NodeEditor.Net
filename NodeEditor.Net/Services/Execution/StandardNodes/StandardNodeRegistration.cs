using System.Collections.Generic;
using System.Linq;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Collects all inline (lambda) standard node definitions.
/// Called during NodeRegistryService initialization.
/// </summary>
public static class StandardNodeRegistration
{
    public static IEnumerable<NodeDefinition> GetInlineDefinitions()
    {
        return StandardNumberNodes.GetDefinitions()
            .Concat(StandardStringNodes.GetDefinitions())
            .Concat(StandardListNodes.GetDefinitions());
    }
}
