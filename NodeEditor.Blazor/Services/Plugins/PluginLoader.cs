using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Editors;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Plugins;

public sealed class PluginLoader
{
    private readonly NodeRegistryService _registry;
    private readonly ILogger<PluginLoader> _logger;
    private readonly PluginOptions _options;
    private readonly IServiceProvider _services;
    private readonly IPluginServiceRegistry _serviceRegistry;
    private readonly INodeContextRegistry? _contextRegistry;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

    public PluginLoader(
        NodeRegistryService registry,
        IOptions<PluginOptions> options,
        ILogger<PluginLoader> logger,
        IServiceProvider services,
        IPluginServiceRegistry serviceRegistry,
        INodeContextRegistry? contextRegistry = null)
    {
        _registry = registry;
        _logger = logger;
        _options = options.Value;
        _services = services;
        _serviceRegistry = serviceRegistry;
        _contextRegistry = contextRegistry;
    }

    public async Task<IReadOnlyList<INodePlugin>> LoadAndRegisterAsync(
        string? pluginDirectory = null,
        IServiceProvider? services = null,
        CancellationToken token = default)
    {
        var plugins = await LoadPluginsAsync(pluginDirectory, token).ConfigureAwait(false);
        services ??= _services;

        foreach (var plugin in plugins)
        {
            try
            {
                await plugin.OnLoadAsync(token).ConfigureAwait(false);

                // Register plugin services and get the plugin's service provider
                var pluginServices = _serviceRegistry.RegisterServices(plugin.Id, plugin.ConfigureServices);

                plugin.Register(_registry);

                if (_loadedPlugins.TryGetValue(plugin.Id, out var registeredEntry))
                {
                    registeredEntry.CustomEditors.AddRange(RegisterCustomEditorsFromAssembly(registeredEntry.Assembly));
                }

                if (plugin is INodeProvider provider)
                {
                    var providedDefinitions = provider.GetNodeDefinitions()?.ToList() ?? new List<NodeDefinition>();
                    if (providedDefinitions.Count > 0)
                    {
                        _registry.RegisterDefinitions(providedDefinitions);
                        if (_loadedPlugins.TryGetValue(plugin.Id, out var entry))
                        {
                            entry.ProviderDefinitions.AddRange(providedDefinitions);
                        }
                    }
                }

                // Initialize plugin with its own service provider containing its configured services
                await plugin.OnInitializeAsync(pluginServices, token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' failed during registration.", plugin.Id);
                try
                {
                    plugin.OnError(ex);
                }
                catch (Exception errorEx)
                {
                    _logger.LogWarning(errorEx, "Plugin '{PluginId}' threw during error handling.", plugin.Id);
                }
            }
        }

        return plugins;
    }

    private List<object> RegisterNodeContextsFromAssembly(IEnumerable<Assembly> assemblies)
    {
        var instances = new List<object>();
        if (_contextRegistry is null) return instances;

        foreach (var assembly in assemblies)
        {
            foreach (var type in SafeGetTypes(assembly))
            {
                if (type is null || type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                // Check both by type assignability and by interface name for cross-context compatibility
                var isNodeContext = typeof(INodeContext).IsAssignableFrom(type) 
                    || typeof(INodeMethodContext).IsAssignableFrom(type)
                    || type.GetInterfaces().Any(i => i.Name == nameof(INodeContext) || i.Name == nameof(INodeMethodContext));

                if (!isNodeContext)
                {
                    continue;
                }

                if (type.GetConstructor(Type.EmptyTypes) is null)
                {
                    continue;
                }

                try
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance is not null)
                    {
                        _contextRegistry.Register(instance);
                        instances.Add(instance);
                        _logger.LogInformation("Registered node context '{ContextType}' from plugin.", type.FullName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create node context instance of type '{ContextType}'.", type.FullName);
                }
            }
        }

        return instances;
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

    public async Task UnloadPluginAsync(string pluginId, CancellationToken token = default)
    {
        if (_loadedPlugins.TryGetValue(pluginId, out var entry))
        {
            try
            {
                await entry.Plugin.OnUnloadAsync(token).ConfigureAwait(false);
                entry.Plugin.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' threw during unload.", pluginId);
            }

            if (_contextRegistry is not null && entry.ContextInstances.Count > 0)
            {
                foreach (var context in entry.ContextInstances)
                {
                    _contextRegistry.Unregister(context);
                }
            }

            if (entry.ProviderDefinitions.Count > 0)
            {
                _registry.RemoveDefinitions(entry.ProviderDefinitions);
            }

            if (entry.CustomEditors.Count > 0)
            {
                var registry = _services.GetService<NodeEditorCustomEditorRegistry>();
                registry?.RemoveEditors(entry.CustomEditors);
            }

            _registry.RemoveDefinitionsFromAssembly(entry.Assembly);

            _serviceRegistry.RemoveServices(pluginId);

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

        return;
    }

    public async Task UnloadAllPluginsAsync(CancellationToken token = default)
    {
        foreach (var (pluginId, entry) in _loadedPlugins.ToList())
        {
            try
            {
                await entry.Plugin.OnUnloadAsync(token).ConfigureAwait(false);
                entry.Plugin.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' threw during unload.", pluginId);
            }

            if (_contextRegistry is not null && entry.ContextInstances.Count > 0)
            {
                foreach (var context in entry.ContextInstances)
                {
                    _contextRegistry.Unregister(context);
                }
            }

            if (entry.ProviderDefinitions.Count > 0)
            {
                _registry.RemoveDefinitions(entry.ProviderDefinitions);
            }

            if (entry.CustomEditors.Count > 0)
            {
                var registry = _services.GetService<NodeEditorCustomEditorRegistry>();
                registry?.RemoveEditors(entry.CustomEditors);
            }

            _registry.RemoveDefinitionsFromAssembly(entry.Assembly);

            _serviceRegistry.RemoveServices(pluginId);

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
        return;
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
                var contexts = RegisterNodeContextsFromAssembly(new[] { assembly });
                _loadedPlugins[plugin.Id] = new LoadedPlugin(plugin, loadContext, assembly, contexts);
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

    private List<INodeCustomEditor> RegisterCustomEditorsFromAssembly(Assembly assembly)
    {
        var registry = _services.GetService<NodeEditorCustomEditorRegistry>();
        if (registry is null)
        {
            return new List<INodeCustomEditor>();
        }

        var editors = new List<INodeCustomEditor>();
        foreach (var type in SafeGetTypes(assembly))
        {
            if (type is null || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (!typeof(INodeCustomEditor).IsAssignableFrom(type))
            {
                continue;
            }

            try
            {
                var editor = ActivatorUtilities.CreateInstance(_services, type) as INodeCustomEditor;
                if (editor is null)
                {
                    continue;
                }

                registry.RegisterEditor(editor);
                editors.Add(editor);
                _logger.LogInformation("Registered custom editor '{EditorType}' from plugin assembly.", type.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create custom editor instance of type '{EditorType}'.", type.FullName);
            }
        }

        return editors;
    }

    private sealed class LoadedPlugin
    {
        public LoadedPlugin(INodePlugin plugin, PluginLoadContext loadContext, Assembly assembly, List<object> contextInstances)
        {
            Plugin = plugin;
            LoadContext = loadContext;
            Assembly = assembly;
            ContextInstances = contextInstances;
        }

        public INodePlugin Plugin { get; }
        public PluginLoadContext LoadContext { get; }
        public Assembly Assembly { get; }
        public List<object> ContextInstances { get; }
        public List<NodeDefinition> ProviderDefinitions { get; } = new();
        public List<INodeCustomEditor> CustomEditors { get; } = new();
    }

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
