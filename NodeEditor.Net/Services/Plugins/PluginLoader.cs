using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services.Logging;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Plugins;

public sealed class PluginLoader : IPluginLoader
{
    private const string DisabledMarkerFileName = ".plugin-disabled";

    private readonly INodeRegistryService _registry;
    private readonly ILogger<PluginLoader> _logger;
    private readonly PluginOptions _options;
    private readonly IServiceProvider _services;
    private readonly IPluginServiceRegistry _serviceRegistry;
    private readonly ILogChannelRegistry? _channelRegistry;
    private readonly Dictionary<string, LoadedPlugin> _loadedPlugins = new(StringComparer.OrdinalIgnoreCase);

    public PluginLoader(
        INodeRegistryService registry,
        IOptions<PluginOptions> options,
        ILogger<PluginLoader> logger,
        IServiceProvider services,
        IPluginServiceRegistry serviceRegistry,
        ILogChannelRegistry? channelRegistry = null)
    {
        _registry = registry;
        _logger = logger;
        _options = options.Value;
        _services = services;
        _serviceRegistry = serviceRegistry;
        _channelRegistry = channelRegistry;
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
                var hostLogger = _services.GetService<INodeEditorLogger>();
                var pluginServices = _serviceRegistry.RegisterServices(plugin.Id, plugin.ConfigureServices, hostLogger);

                plugin.Register(_registry);

                // Let plugin register custom log channels if it implements ILogChannelAware
                if (plugin is ILogChannelAware logAware && _channelRegistry is not null)
                {
                    try
                    {
                        logAware.RegisterChannels(_channelRegistry);
                    }
                    catch (Exception channelEx)
                    {
                        _logger.LogWarning(channelEx, "Plugin '{PluginId}' failed during channel registration.", plugin.Id);
                    }
                }

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
            _loadedPlugins.Remove(pluginId);

            INodePlugin? plugin = entry.Plugin;
            var providerDefinitions = entry.ProviderDefinitions.ToList();
            var customEditors = entry.CustomEditors.ToList();
            Assembly? assembly = entry.Assembly;
            PluginLoadContext? loadContext = entry.LoadContext;

            try
            {
                await plugin.OnUnloadAsync(token).ConfigureAwait(false);
                plugin.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' threw during unload.", pluginId);
            }

            if (providerDefinitions.Count > 0)
            {
                _registry.RemoveDefinitions(providerDefinitions);
            }

            if (customEditors.Count > 0)
            {
                RemoveCustomEditors(customEditors);
            }

            if (assembly is not null)
            {
                _registry.RemoveDefinitionsFromAssembly(assembly);
            }

            _serviceRegistry.RemoveServices(pluginId);

            // Remove log channels registered by this plugin
            _channelRegistry?.RemoveChannelsByPlugin(pluginId);

            var unloadRef = BeginUnload(loadContext, pluginId);

            plugin = null;
            assembly = null;
            loadContext = null;

            if (unloadRef is not null)
            {
                await WaitForUnloadAsync(unloadRef, pluginId, token).ConfigureAwait(false);
            }

            _logger.LogInformation("Plugin '{PluginId}' unloaded.", pluginId);
        }

        return;
    }

    public async Task UnloadAllPluginsAsync(CancellationToken token = default)
    {
        foreach (var (pluginId, entry) in _loadedPlugins.ToList())
        {
            INodePlugin? plugin = entry.Plugin;
            var providerDefinitions = entry.ProviderDefinitions.ToList();
            var customEditors = entry.CustomEditors.ToList();
            Assembly? assembly = entry.Assembly;
            PluginLoadContext? loadContext = entry.LoadContext;

            try
            {
                await plugin.OnUnloadAsync(token).ConfigureAwait(false);
                plugin.Unload();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Plugin '{PluginId}' threw during unload.", pluginId);
            }

            if (providerDefinitions.Count > 0)
            {
                _registry.RemoveDefinitions(providerDefinitions);
            }

            if (customEditors.Count > 0)
            {
                RemoveCustomEditors(customEditors);
            }

            if (assembly is not null)
            {
                _registry.RemoveDefinitionsFromAssembly(assembly);
            }

            _serviceRegistry.RemoveServices(pluginId);

            // Remove log channels registered by this plugin
            _channelRegistry?.RemoveChannelsByPlugin(pluginId);

            var unloadRef = BeginUnload(loadContext, pluginId);

            plugin = null;
            assembly = null;
            loadContext = null;

            if (unloadRef is not null)
            {
                await WaitForUnloadAsync(unloadRef, pluginId, token).ConfigureAwait(false);
            }
        }

        _loadedPlugins.Clear();
        return;
    }

    private WeakReference? BeginUnload(PluginLoadContext? loadContext, string pluginId)
    {
        if (loadContext is null)
        {
            return null;
        }

        try
        {
            var weakReference = new WeakReference(loadContext, trackResurrection: false);
            loadContext.Unload();
            return weakReference;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plugin '{PluginId}' failed to unload context.", pluginId);
            return null;
        }
    }

    private async Task WaitForUnloadAsync(WeakReference unloadReference, string pluginId, CancellationToken token)
    {
        const int maxAttempts = 12;
        for (var attempt = 1; attempt <= maxAttempts && unloadReference.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (!unloadReference.IsAlive)
            {
                break;
            }

            await Task.Delay(50 * attempt, token).ConfigureAwait(false);
        }

        if (unloadReference.IsAlive)
        {
            _logger.LogWarning("Plugin '{PluginId}' load context is still alive after unload attempts.", pluginId);
        }
    }

    public (string PluginId, string PluginName, string? Version)? GetPluginForDefinition(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId))
        {
            return null;
        }

        foreach (var (pluginId, entry) in _loadedPlugins)
        {
            // Check provider definitions registered via INodeProvider
            if (entry.ProviderDefinitions.Any(d => d.Id.Equals(definitionId, StringComparison.Ordinal)))
            {
                return (pluginId, entry.Plugin.Name, entry.Plugin.Version.ToString());
            }

            // Check definitions registered from the plugin assembly via Register()
            var assemblyDefs = _registry.Definitions.Where(d =>
                d.Id.Equals(definitionId, StringComparison.Ordinal));
            foreach (var def in assemblyDefs)
            {
                if (IsDefinitionFromAssembly(def, entry.Assembly))
                {
                    return (pluginId, entry.Plugin.Name, entry.Plugin.Version.ToString());
                }
            }
        }

        return null;
    }

    private static bool IsDefinitionFromAssembly(NodeDefinition definition, Assembly assembly)
    {
        if (definition.NodeType?.Assembly == assembly)
        {
            return true;
        }

        var factoryDeclaringAssembly = definition.Factory.Method.DeclaringType?.Assembly;
        if (factoryDeclaringAssembly == assembly)
        {
            return true;
        }

        var factoryTargetAssembly = definition.Factory.Target?.GetType().Assembly;
        if (factoryTargetAssembly == assembly)
        {
            return true;
        }

        var inlineDeclaringAssembly = definition.InlineExecutor?.Method.DeclaringType?.Assembly;
        return inlineDeclaringAssembly == assembly;
    }

    public IReadOnlyList<(string PluginId, string PluginName, string? Version)> GetLoadedPlugins()
    {
        return _loadedPlugins.Values
            .Select(e => (e.Plugin.Id, e.Plugin.Name, (string?)e.Plugin.Version.ToString()))
            .ToList();
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
                _loadedPlugins[plugin.Id] = new LoadedPlugin(plugin, loadContext, assembly);
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
        var disabledMarkerPath = Path.Combine(directory, DisabledMarkerFileName);
        if (File.Exists(disabledMarkerPath))
        {
            _logger.LogInformation("Skipping disabled plugin directory '{PluginDirectory}'.", directory);
            yield break;
        }

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

    private List<object> RegisterCustomEditorsFromAssembly(Assembly assembly)
    {
        // Use reflection to find the editor registry and INodeCustomEditor interface
        // to avoid compile-time dependency on Blazor-specific types.
        var registryObj = _services.GetService(
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "NodeEditorCustomEditorRegistry") ?? typeof(object));

        if (registryObj is null)
        {
            return new List<object>();
        }

        var editorInterfaceType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.FullName == "NodeEditor.Blazor.Services.Editors.INodeCustomEditor");

        if (editorInterfaceType is null)
        {
            return new List<object>();
        }

        var editors = new List<object>();
        foreach (var type in SafeGetTypes(assembly))
        {
            if (type is null || type.IsAbstract || type.IsInterface)
            {
                continue;
            }

            if (!editorInterfaceType.IsAssignableFrom(type))
            {
                continue;
            }

            try
            {
                var editor = ActivatorUtilities.CreateInstance(_services, type);
                if (editor is null)
                {
                    continue;
                }

                // Call registry.RegisterEditor(editor) via reflection
                var registerMethod = registryObj.GetType().GetMethod("RegisterEditor");
                registerMethod?.Invoke(registryObj, new[] { editor });
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

    private void RemoveCustomEditors(List<object> editors)
    {
        var registryType = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
            .FirstOrDefault(t => t.Name == "NodeEditorCustomEditorRegistry");

        if (registryType is null) return;

        var registryObj = _services.GetService(registryType);
        if (registryObj is null) return;

        var removeMethod = registryType.GetMethod("RemoveEditors");
        removeMethod?.Invoke(registryObj, new object[] { editors });
    }

    private sealed class LoadedPlugin
    {
        public LoadedPlugin(INodePlugin plugin, PluginLoadContext loadContext, Assembly assembly)
        {
            Plugin = plugin;
            LoadContext = loadContext;
            Assembly = assembly;
        }

        public INodePlugin Plugin { get; }
        public PluginLoadContext LoadContext { get; }
        public Assembly Assembly { get; }
        public List<NodeDefinition> ProviderDefinitions { get; } = new();
        public List<object> CustomEditors { get; } = new();
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
