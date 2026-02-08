namespace NodeEditor.Net.Models;

/// <summary>
/// Represents a user-defined graph event.
/// Events enable event-driven execution: a "Custom Event" node listens for
/// a named event, and a "Trigger Event" node fires it, causing all matching
/// listeners to execute their downstream paths.
/// </summary>
public sealed record class GraphEvent(
    string Id,
    string Name)
{
    /// <summary>
    /// Well-known definition ID prefix for Custom Event (listener) nodes.
    /// </summary>
    public const string ListenerDefinitionPrefix = "event.listener.";

    /// <summary>
    /// Well-known definition ID prefix for Trigger Event (sender) nodes.
    /// </summary>
    public const string TriggerDefinitionPrefix = "event.trigger.";

    /// <summary>
    /// Gets the node definition ID for the Custom Event (listener) node.
    /// </summary>
    public string ListenerDefinitionId => ListenerDefinitionPrefix + Id;

    /// <summary>
    /// Gets the node definition ID for the Trigger Event (sender) node.
    /// </summary>
    public string TriggerDefinitionId => TriggerDefinitionPrefix + Id;

    /// <summary>
    /// Creates a new event with a generated ID.
    /// </summary>
    public static GraphEvent Create(string name)
    {
        return new GraphEvent(Guid.NewGuid().ToString("N"), name);
    }
}
