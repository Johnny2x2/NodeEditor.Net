namespace NodeEditor.Net.Services.Plugins;

public sealed class PluginOptions
{
    public const string SectionName = "NodeEditorPlugins";

    public string PluginDirectory { get; set; } = "plugins";

    public Version ApiVersion { get; set; } = new(1, 0, 0);

    public bool EnablePluginLoading { get; set; } = true;

    public string ManifestFileName { get; set; } = "plugin.json";
}
