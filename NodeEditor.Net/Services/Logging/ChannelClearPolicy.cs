namespace NodeEditor.Net.Services.Logging;

/// <summary>
/// Determines when a log channel's entries are automatically cleared.
/// </summary>
public enum ChannelClearPolicy
{
    /// <summary>
    /// Automatically clear entries when graph execution starts.
    /// Default for the built-in Execution and Debug channels.
    /// </summary>
    ClearOnRun,

    /// <summary>
    /// Only clear entries manually (via the Clear button or API call).
    /// Default for the built-in Application channel.
    /// </summary>
    Manual
}
