using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Plugins;

public sealed class PluginLoader
{
    private readonly NodeRegistryService _registry;
    private readonly ILogger<PluginLoader> _logger;
    private readonly PluginOptions _options;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

    public PluginLoader(
        NodeRegistryService registry,
        IOptions<PluginOptions> options,
        ILogger<PluginLoader> logger)
    {
        _registry = registry;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<INodePlugin>> LoadAndRegisterAsync(
        string? pluginDirectory = null,
        CancellationToken token = default)
    {
        var plugins = await LoadPluginsAsync(pluginDirectory, token).ConfigureAwait(false);

        foreach (var plugin in plugins)
        {
            try
            {
                plugin.Register(_registry);

                if (plugin is INodeProvider provider)
                {
                    _registry.RegisterDefinitions(provider.GetNodeDefinitions());
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' failed during registration.", plugin.Id);
            }
        }

        return plugins;
    }

    public Task<IReadOnlyList<INodePlugin>> LoadPluginsAsync(
        string? pluginDirectory = null,
        CancellationToken token = default)
    {
        if (!PlatformGuard.IsPluginLoadingSupported())
        {
            _logger.LogInformation("Plugin loading skipped on unsupported platform (iOS).");
            return Task.FromResult<IReadOnlyList<INodePlugin>>(Array.Empty<INodePlugin>());
        }

        if (!_options.EnablePluginLoading)
        {
            _logger.LogInformation("Plugin loading disabled via configuration.");
            return Task.FromResult<IReadOnlyList<INodePlugin>>(Array.Empty<INodePlugin>());
        }

        var root = ResolvePluginDirectory(pluginDirectory);
        if (!Directory.Exists(root))
        {
            _logger.LogInformation("Plugin directory '{PluginDirectory}' does not exist.", root);
            return Task.FromResult<IReadOnlyList<INodePlugin>>(Array.Empty<INodePlugin>());
        }

        var plugins = new List<INodePlugin>();
        foreach (var candidate in DiscoverCandidates(root))
        {
            token.ThrowIfCancellationRequested();
            LoadCandidate(candidate, plugins);
        }

        return Task.FromResult<IReadOnlyList<INodePlugin>>(plugins);
    }

    public Task UnloadPluginAsync(string pluginId)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var entry))
        {
            try
            {
                entry.Plugin.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' threw during unload.", pluginId);
            }

            try
            {
                entry.LoadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' failed to unload context.", pluginId);
            }

            _loadedPlugins.Remove(pluginId);
            _logger.LogInformation("Plugin '{PluginId}' unloaded.", pluginId);
        }

        return Task.CompletedTask;
    }

    public Task UnloadAllPluginsAsync()
    {
        foreach (var (pluginId, entry) in _loadedPlugins.ToList())
        {
            try
            {
                entry.Plugin.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' threw during unload.", pluginId);
            }

            try
            {
                entry.LoadContext.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' failed to unload context.", pluginId);
            }
        }

        _loadedPlugins.Clear();
        return Task.CompletedTask;
    }

    private string ResolvePluginDirectory(string? pluginDirectory)
    {
        if (!string.IsNullOrWhiteSpace(pluginDirectory))
        {
            return Path.GetFullPath(pluginDirectory);
        }

        return Path.Combine(AppContext.BaseDirectory, _options.PluginDirectory);
    }

    private void LoadCandidate(PluginCandidate candidate, List<INodePlugin> plugins)
    {
        if (!File.Exists(candidate.AssemblyPath))
        {
            _logger.LogWarning("Plugin assembly not found at '{AssemblyPath}'.", candidate.AssemblyPath);
            return;
        }

        var loadContext = new PluginLoadContext(candidate.AssemblyPath);
        Assembly assembly;
        try
        {
            assembly = loadContext.LoadFromAssemblyPath(candidate.AssemblyPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load plugin assembly '{AssemblyPath}'.", candidate.AssemblyPath);
            return;
        }

        var pluginTypes = SafeGetTypes(assembly)
            .Where(type => typeof(INodePlugin).IsAssignableFrom(type)
                           && !type.IsAbstract
                           && type.IsClass)
            .ToList();

        if (pluginTypes.Count == 0)
        {
            _logger.LogInformation("No plugins found in assembly '{AssemblyPath}'.", candidate.AssemblyPath);
            return;
        }

        foreach (var pluginType in pluginTypes)
        {
            try
            {
                if (Activator.CreateInstance(pluginType) is not INodePlugin plugin)
                {
                    continue;
                }

                var validation = Validate(plugin, candidate.Manifest);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Plugin '{PluginId}' rejected: {Message}", plugin.Id, validation.Message);
                    continue;
                }

                if (_loadedPlugins.ContainsKey(plugin.Id))
                {
                    _logger.LogInformation("Plugin '{PluginId}' is already loaded. Skipping duplicate.", plugin.Id);
                    continue;
                }

                plugins.Add(plugin);
                _loadedPlugins[plugin.Id] = new LoadedPlugin(plugin, loadContext);
                _logger.LogInformation("Plugin '{PluginId}' loaded from '{AssemblyPath}'.", plugin.Id, candidate.AssemblyPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create plugin from type '{PluginType}'.", pluginType.FullName);
            }
        }
    }

    private PluginValidationResult Validate(INodePlugin plugin, PluginManifest? manifest)
    {
        if (string.IsNullOrWhiteSpace(plugin.Id))
        {
            return PluginValidationResult.Fail("Plugin Id is required.");
        }

        if (string.IsNullOrWhiteSpace(plugin.Name))
        {
            return PluginValidationResult.Fail("Plugin Name is required.");
        }

        if (plugin.MinApiVersion > _options.ApiVersion)
        {
            return PluginValidationResult.Fail($"Requires API {plugin.MinApiVersion} (host {_options.ApiVersion}).");
        }

        if (manifest is null)
        {
            return PluginValidationResult.Success();
        }

        if (!string.Equals(plugin.Id, manifest.Id, StringComparison.OrdinalIgnoreCase))
        {
            return PluginValidationResult.Fail("Manifest Id does not match plugin Id.");
        }

        if (!string.Equals(plugin.Name, manifest.Name, StringComparison.OrdinalIgnoreCase))
        {
            return PluginValidationResult.Fail("Manifest Name does not match plugin Name.");
        }

        if (Version.TryParse(manifest.MinApiVersion, out var minApi) && minApi > _options.ApiVersion)
        {
            return PluginValidationResult.Fail($"Manifest requires API {minApi} (host {_options.ApiVersion}).");
        }

        return PluginValidationResult.Success();
    }

    private IEnumerable<PluginCandidate> DiscoverCandidates(string root)
    {
        foreach (var directory in Directory.GetDirectories(root))
        {
            foreach (var candidate in DiscoverCandidatesFromDirectory(directory))
            {
                yield return candidate;
            }
        }

        foreach (var candidate in DiscoverCandidatesFromDirectory(root))
        {
            yield return candidate;
        }
    }

    private IEnumerable<PluginCandidate> DiscoverCandidatesFromDirectory(string directory)
    {
        var manifestPath = Path.Combine(directory, _options.ManifestFileName);
        var manifest = PluginManifest.Load(manifestPath);

        if (manifest is not null && !string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var entryAssemblyPath = Path.Combine(directory, manifest.EntryAssembly);
            yield return new PluginCandidate(entryAssemblyPath, manifest);
            yield break;
        }

        foreach (var dll in Directory.GetFiles(directory, "*.dll"))
        {
            yield return new PluginCandidate(dll, manifest);
        }
    }

    private sealed record PluginCandidate(string AssemblyPath, PluginManifest? Manifest);

    private sealed record LoadedPlugin(INodePlugin Plugin, PluginLoadContext LoadContext);

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!
                .Cast<Type>();
        }
    }
}
