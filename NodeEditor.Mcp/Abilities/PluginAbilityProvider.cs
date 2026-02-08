using System.Text.Json;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Plugins.Marketplace;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for managing plugins (install, uninstall, enable, disable).
/// </summary>
public sealed class PluginAbilityProvider : IAbilityProvider
{
    private readonly IPluginLoader _pluginLoader;
    private readonly IPluginInstallationService _installService;

    public PluginAbilityProvider(IPluginLoader pluginLoader, IPluginInstallationService installService)
    {
        _pluginLoader = pluginLoader;
        _installService = installService;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("plugin.list_loaded", "List Loaded Plugins", "Plugins",
            "Lists all currently loaded plugins.",
            "Returns the loaded plugin names and IDs.",
            [],
            ReturnDescription: "Array of loaded plugin info objects."),

        new("plugin.list_installed", "List Installed Plugins", "Plugins",
            "Lists all installed plugins (loaded or not).",
            "Returns installed plugin info including enabled/disabled state.",
            [],
            ReturnDescription: "Array of installed plugin info objects."),

        new("plugin.install", "Install Plugin", "Plugins",
            "Installs a plugin from a local package path.",
            "Provide the path to a plugin package (zip file or directory with plugin.json).",
            [new("packagePath", "string", "Path to the plugin package (zip or directory).")]),

        new("plugin.uninstall", "Uninstall Plugin", "Plugins",
            "Uninstalls a plugin by its ID.",
            "The plugin will be unloaded and its files removed.",
            [new("pluginId", "string", "The ID of the plugin to uninstall.")]),

        new("plugin.enable", "Enable Plugin", "Plugins",
            "Enables a previously disabled plugin.",
            "The plugin will be loaded on next startup.",
            [new("pluginId", "string", "The ID of the plugin to enable.")]),

        new("plugin.disable", "Disable Plugin", "Plugins",
            "Disables a plugin without uninstalling it.",
            "The plugin will be unloaded but its files remain.",
            [new("pluginId", "string", "The ID of the plugin to disable.")]),

        new("plugin.reload", "Reload Plugins", "Plugins",
            "Reloads all plugins from the plugin directory.",
            "Discovers and loads any new or updated plugins.",
            [])
    ];

    public async Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return abilityId switch
        {
            "plugin.list_loaded" => ListLoaded(),
            "plugin.list_installed" => await ListInstalled(cancellationToken),
            "plugin.install" => await Install(parameters, cancellationToken),
            "plugin.uninstall" => await Uninstall(parameters, cancellationToken),
            "plugin.enable" => await Enable(parameters, cancellationToken),
            "plugin.disable" => await Disable(parameters, cancellationToken),
            "plugin.reload" => await Reload(cancellationToken),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        };
    }

    private AbilityResult ListLoaded()
    {
        var plugins = _pluginLoader.GetLoadedPlugins().Select(p => new
        {
            p.PluginId,
            p.PluginName,
            p.Version
        }).ToList();

        return new AbilityResult(true, $"Found {plugins.Count} loaded plugin(s).", Data: plugins);
    }

    private async Task<AbilityResult> ListInstalled(CancellationToken ct)
    {
        var plugins = await _installService.GetInstalledPluginsAsync(ct);
        var list = plugins.Select(p => new
        {
            p.Id,
            p.Name,
            p.Version,
            p.IsEnabled,
            p.IsLoaded,
            p.Author,
            p.Description,
            p.Category,
            p.LoadError
        }).ToList();

        return new AbilityResult(true, $"Found {list.Count} installed plugin(s).", Data: list);
    }

    private async Task<AbilityResult> Install(JsonElement p, CancellationToken ct)
    {
        if (!p.TryGetProperty("packagePath", out var pathEl))
            return new AbilityResult(false, "Missing required parameter 'packagePath'.");

        var result = await _installService.InstallFromPackageAsync(pathEl.GetString()!, ct);
        if (!result.Success)
            return new AbilityResult(false, result.ErrorMessage,
                ErrorHint: $"Error code: {result.ErrorCode}");

        return new AbilityResult(true, $"Plugin '{result.Plugin?.Name}' installed successfully.",
            Data: new { result.Plugin?.Id, result.Plugin?.Name, result.Plugin?.Version });
    }

    private async Task<AbilityResult> Uninstall(JsonElement p, CancellationToken ct)
    {
        if (!p.TryGetProperty("pluginId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'pluginId'.");

        var result = await _installService.UninstallAsync(idEl.GetString()!, ct);
        if (!result.Success)
            return new AbilityResult(false, result.ErrorMessage);

        return new AbilityResult(true, "Plugin uninstalled successfully.");
    }

    private async Task<AbilityResult> Enable(JsonElement p, CancellationToken ct)
    {
        if (!p.TryGetProperty("pluginId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'pluginId'.");

        var success = await _installService.EnablePluginAsync(idEl.GetString()!, ct);
        return success
            ? new AbilityResult(true, "Plugin enabled.")
            : new AbilityResult(false, "Plugin not found or could not be enabled.");
    }

    private async Task<AbilityResult> Disable(JsonElement p, CancellationToken ct)
    {
        if (!p.TryGetProperty("pluginId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'pluginId'.");

        var success = await _installService.DisablePluginAsync(idEl.GetString()!, ct);
        return success
            ? new AbilityResult(true, "Plugin disabled.")
            : new AbilityResult(false, "Plugin not found or could not be disabled.");
    }

    private async Task<AbilityResult> Reload(CancellationToken ct)
    {
        await _pluginLoader.LoadAndRegisterAsync(token: ct);
        var loaded = _pluginLoader.GetLoadedPlugins();
        return new AbilityResult(true, $"Plugins reloaded. {loaded.Count} plugin(s) loaded.");
    }
}
