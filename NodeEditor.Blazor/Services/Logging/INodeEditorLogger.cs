namespace NodeEditor.Blazor.Services.Logging;

/// <summary>
/// Central logging service for the node editor.
/// Supports multiple named channels with per-channel ring buffers.
/// </summary>
public interface INodeEditorLogger
{
    /// <summary>
    /// Writes a log entry to the specified channel.
    /// If the channel is not registered, the entry is silently dropped.
    /// </summary>
    void Log(string channel, LogLevel level, string message, string? nodeId = null, string? nodeName = null, object? payload = null);

    /// <summary>
    /// Writes a Debug-level entry to the Debug channel.
    /// </summary>
    void LogDebug(string message, string? nodeId = null, string? nodeName = null);

    /// <summary>
    /// Writes an Info-level entry to the Execution channel.
    /// </summary>
    void LogExecution(string message, string? nodeId = null, string? nodeName = null);

    /// <summary>
    /// Writes an Info-level entry to the Application channel.
    /// </summary>
    void LogApplication(string message);

    /// <summary>
    /// Gets all entries for a specific channel, ordered oldest-first.
    /// </summary>
    IReadOnlyList<LogEntry> GetEntries(string channel);

    /// <summary>
    /// Clears entries for a specific channel, or all channels if null.
    /// </summary>
    void Clear(string? channel = null);

    /// <summary>
    /// Clears all channels whose <see cref="ChannelClearPolicy"/> is <see cref="ChannelClearPolicy.ClearOnRun"/>.
    /// Called automatically at the start of each execution run.
    /// </summary>
    void ClearOnRun();

    /// <summary>
    /// Raised whenever a new log entry is written.
    /// </summary>
    event Action<LogEntry>? OnLogEntry;

    /// <summary>
    /// Raised whenever entries are cleared (channel name, or null for all).
    /// </summary>
    event Action<string?>? OnEntriesCleared;
}
