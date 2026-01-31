using System.Reflection;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Blazor.Services.Registry;

public sealed class NodeRegistryService
{
    private readonly NodeDiscoveryService _discovery;
    private readonly List<NodeDefinition> _definitions = new();
    private readonly object _lock = new();
    private bool _initialized;

    public NodeRegistryService(NodeDiscoveryService discovery)
    {
        _discovery = discovery;
    }

    public event EventHandler? RegistryChanged;

    public IReadOnlyList<NodeDefinition> Definitions
    {
        get
        {
            EnsureInitialized();
            return _definitions;
        }
    }

    public void EnsureInitialized(IEnumerable<Assembly>? assemblies = null)
    {
        lock (_lock)
        {
            if (_initialized)
            {
                return;
            }

            var scanAssemblies = assemblies?.ToArray() ?? AppDomain.CurrentDomain.GetAssemblies();
            var discovered = _discovery.DiscoverFromAssemblies(scanAssemblies);
            MergeDefinitions(discovered);
            _initialized = true;
        }
    }

    public void RegisterFromAssembly(Assembly assembly)
    {
        if (assembly is null)
        {
            throw new ArgumentNullException(nameof(assembly));
        }

        var discovered = _discovery.DiscoverFromAssemblies(new[] { assembly });
        MergeDefinitions(discovered);
        _initialized = true;
    }

    public void RegisterPluginAssembly(Assembly assembly)
    {
        PlatformGuard.ThrowIfPluginLoadingUnsupported();
        RegisterFromAssembly(assembly);
    }

    public void RegisterDefinitions(IEnumerable<NodeDefinition> definitions)
    {
        if (definitions is null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }

        MergeDefinitions(definitions);
        _initialized = true;
    }

    public NodeCatalog GetCatalog(string? search = null)
    {
        var definitions = Definitions;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var query = search.Trim();
            definitions = definitions
                .Where(def => MatchesQuery(def, query))
                .ToList();
        }

        return NodeCatalog.Create(definitions);
    }

    private void MergeDefinitions(IEnumerable<NodeDefinition> definitions)
    {
        var added = false;
        lock (_lock)
        {
            foreach (var definition in definitions)
            {
                if (_definitions.Any(d => d.Id.Equals(definition.Id, StringComparison.Ordinal)))
                {
                    continue;
                }

                _definitions.Add(definition);
                added = true;
            }
        }

        if (added)
        {
            RegistryChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private static bool MatchesQuery(NodeDefinition definition, string query)
    {
        return definition.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
               || definition.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
               || definition.Description.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
