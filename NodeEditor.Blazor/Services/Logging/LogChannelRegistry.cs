namespace NodeEditor.Blazor.Services.Logging;

/// <summary>
/// Default implementation of <see cref="ILogChannelRegistry"/>.
/// Thread-safe. Registered as singleton.
/// </summary>
public sealed class LogChannelRegistry : ILogChannelRegistry
{
    private readonly object _lock = new();
    private readonly Dictionary<string, LogChannelRegistration> _channels = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BuiltInChannels = new(StringComparer.OrdinalIgnoreCase)
    {
        LogChannels.Execution,
        LogChannels.Debug,
        LogChannels.Application
    };

    public LogChannelRegistry()
    {
        // Register built-in channels
        _channels[LogChannels.Execution] = new LogChannelRegistration(LogChannels.Execution, null, ChannelClearPolicy.ClearOnRun);
        _channels[LogChannels.Debug] = new LogChannelRegistration(LogChannels.Debug, null, ChannelClearPolicy.ClearOnRun);
        _channels[LogChannels.Application] = new LogChannelRegistration(LogChannels.Application, null, ChannelClearPolicy.Manual);
    }

    public event Action? ChannelsChanged;

    public IReadOnlyList<LogChannelRegistration> Channels
    {
        get
        {
            lock (_lock)
            {
                return _channels.Values.ToList();
            }
        }
    }

    public LogChannelRegistration? GetChannel(string channelName)
    {
        lock (_lock)
        {
            return _channels.TryGetValue(channelName, out var reg) ? reg : null;
        }
    }

    public bool RegisterChannel(string channelName, string? pluginId = null, ChannelClearPolicy clearPolicy = ChannelClearPolicy.Manual)
    {
        if (string.IsNullOrWhiteSpace(channelName))
        {
            throw new ArgumentException("Channel name is required.", nameof(channelName));
        }

        lock (_lock)
        {
            if (_channels.ContainsKey(channelName))
            {
                return false;
            }

            _channels[channelName] = new LogChannelRegistration(channelName, pluginId, clearPolicy);
        }

        ChannelsChanged?.Invoke();
        return true;
    }

    public bool RemoveChannel(string channelName)
    {
        if (BuiltInChannels.Contains(channelName))
        {
            return false;
        }

        bool removed;
        lock (_lock)
        {
            removed = _channels.Remove(channelName);
        }

        if (removed)
        {
            ChannelsChanged?.Invoke();
        }

        return removed;
    }

    public void RemoveChannelsByPlugin(string pluginId)
    {
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return;
        }

        var removed = false;

        lock (_lock)
        {
            var toRemove = _channels
                .Where(kvp => string.Equals(kvp.Value.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)
                              && !BuiltInChannels.Contains(kvp.Key))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in toRemove)
            {
                _channels.Remove(key);
                removed = true;
            }
        }

        if (removed)
        {
            ChannelsChanged?.Invoke();
        }
    }
}
