using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Registry for node context instances. Plugins register their INodeContext
/// implementations here so they can be discovered during execution.
/// </summary>
public interface INodeContextRegistry
{
    /// <summary>
    /// Registers a node context instance.
    /// </summary>
    void Register(object context);

    /// <summary>
    /// Registers a node context type. An instance will be created on demand.
    /// </summary>
    void Register<TContext>() where TContext : class, new();

    /// <summary>
    /// Removes a previously registered context.
    /// </summary>
    void Unregister(object context);

    /// <summary>
    /// Gets all registered context instances.
    /// </summary>
    IReadOnlyList<object> GetContexts();

    /// <summary>
    /// Creates a CompositeNodeContext from all registered contexts.
    /// </summary>
    CompositeNodeContext CreateCompositeContext();
}

/// <summary>
/// Default implementation of INodeContextRegistry.
/// </summary>
public sealed class NodeContextRegistry : INodeContextRegistry
{
    private readonly List<object> _instances = new();
    private readonly List<Type> _types = new();
    private readonly object _lock = new();
    private readonly INodeContextFactory _contextFactory;

    public NodeContextRegistry(INodeContextFactory contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
    }

    public void Register(object context)
    {
        if (context is null) return;
        
        lock (_lock)
        {
            if (!_instances.Contains(context))
            {
                _instances.Add(context);
            }
        }
    }

    public void Register<TContext>() where TContext : class, new()
    {
        lock (_lock)
        {
            if (!_types.Contains(typeof(TContext)))
            {
                _types.Add(typeof(TContext));
            }
        }
    }

    public void Unregister(object context)
    {
        if (context is null) return;
        
        lock (_lock)
        {
            _instances.Remove(context);
        }
    }

    public IReadOnlyList<object> GetContexts()
    {
        lock (_lock)
        {
            var contexts = new List<object>(_instances);
            
            foreach (var type in _types)
            {
                try
                {
                    var instance = Activator.CreateInstance(type);
                    if (instance is not null)
                    {
                        contexts.Add(instance);
                    }
                }
                catch
                {
                    // Ignore types that cannot be instantiated
                }
            }
            
            return contexts;
        }
    }

    public CompositeNodeContext CreateCompositeContext()
    {
        // Get registered contexts
        var contexts = GetContexts().ToList();
        
        // Also include contexts from loaded assemblies for backward compatibility
        var assemblyContexts = _contextFactory.CreateCompositeFromLoadedAssemblies();
        foreach (var ctx in assemblyContexts.Contexts)
        {
            if (!contexts.Any(c => c.GetType() == ctx.GetType()))
            {
                contexts.Add(ctx);
            }
        }
        
        return new CompositeNodeContext(contexts);
    }
}
