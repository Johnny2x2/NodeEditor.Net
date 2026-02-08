namespace NodeEditor.Blazor.Services.Logging;

/// <summary>
/// Describes a registered log channel.
/// </summary>
public sealed record LogChannelRegistration(
    string Name,
    string? PluginId,
    ChannelClearPolicy ClearPolicy = ChannelClearPolicy.Manual);
