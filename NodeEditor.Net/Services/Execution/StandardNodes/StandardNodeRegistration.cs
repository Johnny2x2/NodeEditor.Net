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
            .Concat(StandardMathNodes.GetDefinitions())
            .Concat(StandardMathExtraNodes.GetDefinitions())
            .Concat(StandardStringNodes.GetDefinitions())
            .Concat(StandardStringExtraNodes.GetDefinitions())
            .Concat(StandardListNodes.GetDefinitions())
            .Concat(StandardListExtraNodes.GetDefinitions())
            .Concat(StandardLogicNodes.GetDefinitions())
            .Concat(StandardConversionNodes.GetDefinitions())
            .Concat(StandardDateTimeNodes.GetDefinitions())
            .Concat(StandardConstantNodes.GetDefinitions())
            .Concat(StandardDictNodes.GetDefinitions())
            .Concat(StandardRandomNodes.GetDefinitions())
            .Concat(StandardJsonNodes.GetDefinitions());
    }
}
