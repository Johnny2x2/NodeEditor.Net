namespace NodeEditor.Net.Services.Logging;

/// <summary>
/// Default implementation of <see cref="INodeEditorLogger"/>.
/// Uses per-channel ring buffers (capped at <see cref="MaxEntriesPerChannel"/>).
/// Thread-safe. Registered as singleton.
/// </summary>
public sealed class NodeEditorLogger : INodeEditorLogger
{
    private const int MaxEntriesPerChannel = 1000;

    private readonly ILogChannelRegistry _channelRegistry;
    private readonly object _lock = new();
    private readonly Dictionary<string, LinkedList<LogEntry>> _buffers = new(StringComparer.OrdinalIgnoreCase);

    public NodeEditorLogger(ILogChannelRegistry channelRegistry)
    {
        _channelRegistry = channelRegistry ?? throw new ArgumentNullException(nameof(channelRegistry));
    }

    public event Action<LogEntry>? OnLogEntry;
    public event Action<string?>? OnEntriesCleared;

    public void Log(string channel, LogLevel level, string message, string? nodeId = null, string? nodeName = null, object? payload = null)
    {
        // Allow writing to any channel, registered or not — this enables
        // dynamic channel creation by plugins that write before explicitly registering.
        var entry = new LogEntry(DateTime.Now, level, channel, message, nodeId, nodeName, payload);

        lock (_lock)
        {
            if (!_buffers.TryGetValue(channel, out var buffer))
            {
                buffer = new LinkedList<LogEntry>();
                _buffers[channel] = buffer;
            }

            buffer.AddLast(entry);

            while (buffer.Count > MaxEntriesPerChannel)
            {
                buffer.RemoveFirst();
            }
        }

        OnLogEntry?.Invoke(entry);
    }

    public void LogDebug(string message, string? nodeId = null, string? nodeName = null)
    {
        Log(LogChannels.Debug, LogLevel.Debug, message, nodeId, nodeName);
    }

    public void LogExecution(string message, string? nodeId = null, string? nodeName = null)
    {
        Log(LogChannels.Execution, LogLevel.Info, message, nodeId, nodeName);
    }

    public void LogApplication(string message)
    {
        Log(LogChannels.Application, LogLevel.Info, message);
    }

    public IReadOnlyList<LogEntry> GetEntries(string channel)
    {
        lock (_lock)
        {
            if (_buffers.TryGetValue(channel, out var buffer))
            {
                return buffer.ToList();
            }
        }

        return Array.Empty<LogEntry>();
    }

    public void Clear(string? channel = null)
    {
        lock (_lock)
        {
            if (channel is not null)
            {
                if (_buffers.TryGetValue(channel, out var buffer))
                {
                    buffer.Clear();
                }
            }
            else
            {
                foreach (var buffer in _buffers.Values)
                {
                    buffer.Clear();
                }
            }
        }

        OnEntriesCleared?.Invoke(channel);
    }

    public void ClearOnRun()
    {
        var channelsToClear = _channelRegistry.Channels
            .Where(c => c.ClearPolicy == ChannelClearPolicy.ClearOnRun)
            .Select(c => c.Name)
            .ToList();

        lock (_lock)
        {
            foreach (var channelName in channelsToClear)
            {
                if (_buffers.TryGetValue(channelName, out var buffer))
                {
                    buffer.Clear();
                }
            }
        }

        foreach (var channelName in channelsToClear)
        {
            OnEntriesCleared?.Invoke(channelName);
        }
    }
}
