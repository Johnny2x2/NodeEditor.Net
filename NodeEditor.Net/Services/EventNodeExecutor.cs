using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Net.Services;

/// <summary>
/// Handles execution of Custom Event and Trigger Event nodes during graph execution.
/// Event nodes don't have backing [Node]-attributed methods; instead they interact with
/// the <see cref="ExecutionEventBus"/> on the execution context.
/// </summary>
public static class EventNodeExecutor
{
    /// <summary>
    /// Returns true if the given node is an event node (Listener or Trigger).
    /// </summary>
    public static bool IsEventNode(NodeData node)
    {
        if (string.IsNullOrEmpty(node.DefinitionId))
            return false;

        return node.DefinitionId.StartsWith(GraphEvent.ListenerDefinitionPrefix, StringComparison.Ordinal)
               || node.DefinitionId.StartsWith(GraphEvent.TriggerDefinitionPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// Extracts the event ID from an event node's definition ID.
    /// </summary>
    public static string? GetEventId(NodeData node)
    {
        if (string.IsNullOrEmpty(node.DefinitionId))
            return null;

        if (node.DefinitionId.StartsWith(GraphEvent.ListenerDefinitionPrefix, StringComparison.Ordinal))
            return node.DefinitionId[GraphEvent.ListenerDefinitionPrefix.Length..];

        if (node.DefinitionId.StartsWith(GraphEvent.TriggerDefinitionPrefix, StringComparison.Ordinal))
            return node.DefinitionId[GraphEvent.TriggerDefinitionPrefix.Length..];

        return null;
    }

    /// <summary>
    /// Returns true if the node is a Custom Event (listener) node.
    /// </summary>
    public static bool IsListenerNode(NodeData node)
    {
        return node.DefinitionId?.StartsWith(GraphEvent.ListenerDefinitionPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Returns true if the node is a Trigger Event (sender) node.
    /// </summary>
    public static bool IsTriggerNode(NodeData node)
    {
        return node.DefinitionId?.StartsWith(GraphEvent.TriggerDefinitionPrefix, StringComparison.Ordinal) == true;
    }

    /// <summary>
    /// Executes a Custom Event listener node by signaling its Exit path.
    /// Called by the event bus handler when the event is triggered.
    /// </summary>
    public static void ExecuteListener(NodeData node, INodeRuntimeStorage context)
    {
        // Signal execution path (boolean marker — the execution engine follows
        // connections via GetExecutionTargets, not socket values)
        context.SetSocketValue(node.Id, "Exit", true);
        context.MarkNodeExecuted(node.Id);
    }

    /// <summary>
    /// Executes a Trigger Event node by firing the named event on the bus
    /// and signaling its own Exit path.
    /// </summary>
    public static async Task ExecuteTriggerAsync(
        NodeData node,
        INodeRuntimeStorage context,
        CancellationToken token)
    {
        var eventId = GetEventId(node)
            ?? throw new InvalidOperationException($"Cannot extract event ID from node '{node.Name}'.");

        // Fire the event — all registered listeners will execute
        await context.EventBus.TriggerAsync(eventId, token).ConfigureAwait(false);

        // Signal our own Exit path to continue the triggering execution chain
        context.SetSocketValue(node.Id, "Exit", true);
        context.MarkNodeExecuted(node.Id);
    }
}
