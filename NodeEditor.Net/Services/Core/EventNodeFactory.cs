using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services;

/// <summary>
/// Creates and manages Custom Event (listener) and Trigger Event (sender) node definitions
/// in the registry whenever graph events are added, removed, or changed.
/// Mirrors the pattern used by <see cref="VariableNodeFactory"/> for variables.
/// </summary>
public sealed class EventNodeFactory
{
    private readonly INodeRegistryService _registry;
    private readonly INodeEditorState _state;

    public EventNodeFactory(INodeRegistryService registry, INodeEditorState state)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _state = state ?? throw new ArgumentNullException(nameof(state));

        _state.EventAdded += OnEventAdded;
        _state.EventRemoved += OnEventRemoved;
        _state.EventChanged += OnEventChanged;

        // Register definitions for any events already present
        foreach (var graphEvent in _state.Events)
        {
            RegisterEventDefinitions(graphEvent);
        }
    }

    private void OnEventAdded(object? sender, GraphEventEventArgs e)
    {
        RegisterEventDefinitions(e.Event);
    }

    private void OnEventRemoved(object? sender, GraphEventEventArgs e)
    {
        UnregisterEventDefinitions(e.Event);
    }

    private void OnEventChanged(object? sender, GraphEventChangedEventArgs e)
    {
        UnregisterEventDefinitions(e.PreviousEvent);
        RegisterEventDefinitions(e.CurrentEvent);
    }

    private void RegisterEventDefinitions(GraphEvent graphEvent)
    {
        var listenerDef = BuildListenerDefinition(graphEvent);
        var triggerDef = BuildTriggerDefinition(graphEvent);
        _registry.RegisterDefinitions(new[] { listenerDef, triggerDef });
    }

    private void UnregisterEventDefinitions(GraphEvent graphEvent)
    {
        var stubs = new[]
        {
            new NodeDefinition(graphEvent.ListenerDefinitionId, "", "", "", Array.Empty<SocketData>(), Array.Empty<SocketData>(), () => null!),
            new NodeDefinition(graphEvent.TriggerDefinitionId, "", "", "", Array.Empty<SocketData>(), Array.Empty<SocketData>(), () => null!)
        };
        _registry.RemoveDefinitions(stubs);
    }

    /// <summary>
    /// Builds a "Custom Event: {Name}" listener node definition.
    /// This is an execution initiator (no Enter socket) — it fires when triggered by a Trigger Event node.
    /// </summary>
    private static NodeDefinition BuildListenerDefinition(GraphEvent graphEvent)
    {
        var execType = typeof(ExecutionPath).FullName!;

        var outputs = new List<SocketData>
        {
            new SocketData("Exit", execType, IsInput: false, IsExecution: true)
        };

        return new NodeDefinition(
            Id: graphEvent.ListenerDefinitionId,
            Name: "Custom Event: " + graphEvent.Name,
            Category: "Events",
            Description: $"Listens for the '{graphEvent.Name}' event. Execution flows from Exit when this event is triggered.",
            Inputs: Array.Empty<SocketData>(),
            Outputs: outputs,
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString("N"),
                Name: "Custom Event: " + graphEvent.Name,
                Callable: true,
                ExecInit: true,
                Inputs: Array.Empty<SocketData>(),
                Outputs: outputs,
                DefinitionId: graphEvent.ListenerDefinitionId));
    }

    /// <summary>
    /// Builds a "Trigger Event: {Name}" sender node definition.
    /// This is a callable node (Enter/Exit) that fires the named event when executed.
    /// </summary>
    private static NodeDefinition BuildTriggerDefinition(GraphEvent graphEvent)
    {
        var execType = typeof(ExecutionPath).FullName!;

        var inputs = new List<SocketData>
        {
            new SocketData("Enter", execType, IsInput: true, IsExecution: true)
        };

        var outputs = new List<SocketData>
        {
            new SocketData("Exit", execType, IsInput: false, IsExecution: true)
        };

        return new NodeDefinition(
            Id: graphEvent.TriggerDefinitionId,
            Name: "Trigger Event: " + graphEvent.Name,
            Category: "Events",
            Description: $"Triggers the '{graphEvent.Name}' event. All matching Custom Event listeners will execute.",
            Inputs: inputs,
            Outputs: outputs,
            Factory: () => new NodeData(
                Id: Guid.NewGuid().ToString("N"),
                Name: "Trigger Event: " + graphEvent.Name,
                Callable: true,
                ExecInit: false,
                Inputs: inputs,
                Outputs: outputs,
                DefinitionId: graphEvent.TriggerDefinitionId));
    }
}
