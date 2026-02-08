namespace NodeEditor.Blazor.Services.Logging;

/// <summary>
/// Registry for log channels. Manages channel registration and lifecycle.
/// Built-in channels (Execution, Debug, Application) are always present.
/// Plugins can register custom channels via <see cref="NodeEditor.Blazor.Services.Plugins.ILogChannelAware"/>.
/// </summary>
public interface ILogChannelRegistry
{
    /// <summary>
    /// Registers a new log channel.
    /// </summary>
    /// <param name="channelName">Unique channel name.</param>
    /// <param name="pluginId">The plugin that owns this channel, or null for built-in channels.</param>
    /// <param name="clearPolicy">When to automatically clear the channel's entries.</param>
    /// <returns>True if registered; false if the channel name already exists.</returns>
    bool RegisterChannel(string channelName, string? pluginId = null, ChannelClearPolicy clearPolicy = ChannelClearPolicy.Manual);

    /// <summary>
    /// Removes a channel by name. Built-in channels cannot be removed.
    /// </summary>
    bool RemoveChannel(string channelName);

    /// <summary>
    /// Removes all channels registered by a specific plugin.
    /// </summary>
    void RemoveChannelsByPlugin(string pluginId);

    /// <summary>
    /// Gets all registered channel registrations.
    /// </summary>
    IReadOnlyList<LogChannelRegistration> Channels { get; }

    /// <summary>
    /// Gets the registration for a specific channel, or null if not found.
    /// </summary>
    LogChannelRegistration? GetChannel(string channelName);

    /// <summary>
    /// Raised when the set of registered channels changes.
    /// </summary>
    event Action? ChannelsChanged;
}
