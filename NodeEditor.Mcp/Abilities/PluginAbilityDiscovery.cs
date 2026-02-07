using System.Text.Json;
using NodeEditor.Blazor.Services.Plugins;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Optional interface that plugins can implement to contribute MCP abilities.
/// When a plugin assembly contains a type implementing this interface,
/// the PluginAbilityDiscovery service will pick it up and register its abilities.
/// </summary>
public interface IPluginAbilityContributor
{
    /// <summary>Source name for these abilities (typically the plugin name).</summary>
    string Source { get; }

    /// <summary>Returns the abilities this plugin contributes.</summary>
    IReadOnlyList<AbilityDescriptor> GetAbilities();

    /// <summary>Executes an ability by its id with the given JSON parameters.</summary>
    Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Discovers and loads ability providers from loaded plugins.
/// Scans plugin assemblies for IPluginAbilityContributor implementations.
/// </summary>
public sealed class PluginAbilityDiscovery
{
    private readonly IPluginLoader _pluginLoader;
    private readonly AbilityRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly HashSet<string> _registeredPlugins = new(StringComparer.OrdinalIgnoreCase);

    public PluginAbilityDiscovery(IPluginLoader pluginLoader, AbilityRegistry registry, IServiceProvider services)
    {
        _pluginLoader = pluginLoader;
        _registry = registry;
        _services = services;
    }

    /// <summary>
    /// Scans loaded plugins for IPluginAbilityContributor implementations and registers them.
    /// Safe to call multiple times; already-registered plugins are skipped.
    /// </summary>
    public void DiscoverAndRegister()
    {
        foreach (var (pluginId, _, _) in _pluginLoader.GetLoadedPlugins())
        {
            if (_registeredPlugins.Contains(pluginId))
                continue;

            try
            {
                // Get plugin assembly via the definition mapping
                var definitions = _services.GetService(typeof(NodeEditor.Blazor.Services.Registry.INodeRegistryService))
                    as NodeEditor.Blazor.Services.Registry.INodeRegistryService;
                if (definitions is null) continue;

                definitions.EnsureInitialized();
                foreach (var def in definitions.Definitions)
                {
                    var ownerInfo = _pluginLoader.GetPluginForDefinition(def.Id);
                    if (ownerInfo?.PluginId != pluginId) continue;

                    // Found a definition from this plugin, get its assembly
                    var assembly = def.Factory().GetType().Assembly;
                    var contributorTypes = assembly.GetTypes()
                        .Where(t => typeof(IPluginAbilityContributor).IsAssignableFrom(t)
                                    && !t.IsAbstract && !t.IsInterface);

                    foreach (var type in contributorTypes)
                    {
                        if (Activator.CreateInstance(type) is IPluginAbilityContributor contributor)
                        {
                            var adapter = new PluginAbilityAdapter(contributor);
                            _registry.Register(adapter);
                        }
                    }

                    break; // Only need one definition to get the assembly
                }

                _registeredPlugins.Add(pluginId);
            }
            catch
            {
                // Plugin doesn't have ability contributors — that's fine
                _registeredPlugins.Add(pluginId);
            }
        }
    }

    /// <summary>
    /// Adapter that wraps an IPluginAbilityContributor as an IAbilityProvider.
    /// </summary>
    private sealed class PluginAbilityAdapter : IAbilityProvider
    {
        private readonly IPluginAbilityContributor _contributor;

        public PluginAbilityAdapter(IPluginAbilityContributor contributor)
        {
            _contributor = contributor;
        }

        public string Source => _contributor.Source;

        public IReadOnlyList<AbilityDescriptor> GetAbilities() => _contributor.GetAbilities();

        public Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
            => _contributor.ExecuteAsync(abilityId, parameters, cancellationToken);
    }
}
