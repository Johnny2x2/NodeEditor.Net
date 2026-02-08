using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services;

/// <summary>
/// Creates and manages Get/Set Variable node definitions in the registry
/// whenever graph variables are added, removed, or changed.
/// </summary>
public sealed class VariableNodeFactory
{
    private readonly INodeRegistryService _registry;
    private readonly INodeEditorState _state;

    public VariableNodeFactory(INodeRegistryService registry, INodeEditorState state)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _state = state ?? throw new ArgumentNullException(nameof(state));

        _state.VariableAdded += OnVariableAdded;
        _state.VariableRemoved += OnVariableRemoved;
        _state.VariableChanged += OnVariableChanged;

        // Register definitions for any variables already present
        foreach (var variable in _state.Variables)
        {
            RegisterVariableDefinitions(variable);
        }
    }

    private void OnVariableAdded(object? sender, GraphVariableEventArgs e)
    {
        RegisterVariableDefinitions(e.Variable);
    }

    private void OnVariableRemoved(object? sender, GraphVariableEventArgs e)
    {
        UnregisterVariableDefinitions(e.Variable);
    }

    private void OnVariableChanged(object? sender, GraphVariableChangedEventArgs e)
    {
        UnregisterVariableDefinitions(e.PreviousVariable);
        RegisterVariableDefinitions(e.CurrentVariable);
    }

    private void RegisterVariableDefinitions(GraphVariable variable)
    {
        var getDefinition = BuildGetDefinition(variable);
        var setDefinition = BuildSetDefinition(variable);
        _registry.RegisterDefinitions(new[] { getDefinition, setDefinition });
    }

    private void UnregisterVariableDefinitions(GraphVariable variable)
    {
        var getDefId = variable.GetDefinitionId;
        var setDefId = variable.SetDefinitionId;

        // Build stubs with matching IDs so RemoveDefinitions can find them
        var stubs = new[]
        {
            new NodeDefinition(getDefId, "", "", "", Array.Empty<SocketData>(), Array.Empty<SocketData>(), () => null!),
            new NodeDefinition(setDefId, "", "", "", Array.Empty<SocketData>(), Array.Empty<SocketData>(), () => null!)
        };
        _registry.RemoveDefinitions(stubs);
    }

    /// <summary>
    /// Builds a "Get {Name}" node definition for a variable.
    /// The node has one output socket matching the variable's type.
    /// </summary>
    private static NodeDefinition BuildGetDefinition(GraphVariable variable)
    {
        var outputs = new List<SocketData>
        {
            new SocketData("Value", variable.TypeName, IsInput: false, IsExecution: false, variable.DefaultValue)
        };

        return new NodeDefinition(
            Id: variable.GetDefinitionId,
            Name: "Get " + variable.Name,
            Category: "Variables",
            Description: $"Gets the current value of variable '{variable.Name}'.",
            Inputs: Array.Empty<SocketData>(),
            Outputs: outputs,
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString("N"),
                Name: "Get " + variable.Name,
                Callable: false,
                ExecInit: false,
                Inputs: Array.Empty<SocketData>(),
                Outputs: outputs,
                DefinitionId: variable.GetDefinitionId));
    }

    /// <summary>
    /// Builds a "Set {Name}" node definition for a variable.
    /// The node is callable (has Enter/Exit execution sockets) and
    /// has one input socket for the new value plus one output socket (pass-through).
    /// </summary>
    private static NodeDefinition BuildSetDefinition(GraphVariable variable)
    {
        var inputs = new List<SocketData>
        {
            new SocketData("Enter", typeof(ExecutionPath).FullName!, IsInput: true, IsExecution: true),
            new SocketData("Value", variable.TypeName, IsInput: true, IsExecution: false, variable.DefaultValue)
        };

        var outputs = new List<SocketData>
        {
            new SocketData("Exit", typeof(ExecutionPath).FullName!, IsInput: false, IsExecution: true),
            new SocketData("Value", variable.TypeName, IsInput: false, IsExecution: false)
        };

        return new NodeDefinition(
            Id: variable.SetDefinitionId,
            Name: "Set " + variable.Name,
            Category: "Variables",
            Description: $"Sets the value of variable '{variable.Name}'.",
            Inputs: inputs,
            Outputs: outputs,
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString("N"),
                Name: "Set " + variable.Name,
                Callable: true,
                ExecInit: false,
                Inputs: inputs,
                Outputs: outputs,
                DefinitionId: variable.SetDefinitionId));
    }
}
