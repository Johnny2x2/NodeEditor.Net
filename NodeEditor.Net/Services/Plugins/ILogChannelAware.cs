using NodeEditor.Net.Services.Logging;

namespace NodeEditor.Net.Services.Plugins;

/// <summary>
/// Optional interface for plugins that want to register custom log channels.
/// Implement this on your <see cref="INodePlugin"/> class to register channels
/// that appear in the Output Terminal dropdown.
/// </summary>
/// <remarks>
/// This is a non-breaking opt-in interface. Existing plugins that do not implement
/// it are unaffected. The <see cref="ILogChannelRegistry"/> is the host singleton,
/// so channels registered here are visible across the entire editor.
/// </remarks>
public interface ILogChannelAware
{
    /// <summary>
    /// Called by the plugin loader after <see cref="INodePlugin.Register"/> and before
    /// <see cref="INodePlugin.OnInitializeAsync"/>. Register any custom log channels here.
    /// </summary>
    /// <param name="registry">The host log channel registry.</param>
    void RegisterChannels(ILogChannelRegistry registry);
}
