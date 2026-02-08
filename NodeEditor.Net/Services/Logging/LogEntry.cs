namespace NodeEditor.Net.Services.Logging;

/// <summary>
/// Represents a single log entry in the output terminal.
/// </summary>
public sealed record LogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Channel,
    string Message,
    string? NodeId = null,
    string? NodeName = null,
    object? Payload = null);
