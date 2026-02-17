namespace NodeEditor.Net.Models;

/// <summary>
/// Request data for creating an event node from the Events panel.
/// </summary>
/// <param name="EventId">The graph event ID.</param>
/// <param name="IsTrigger">True for Trigger Event node, false for Custom Event (listener) node.</param>
public sealed record class EventNodeRequest(string EventId, bool IsTrigger);
