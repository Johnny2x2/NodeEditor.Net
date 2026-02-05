using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Serialization;
using NodeEditor.Blazor.Services.Plugins.Marketplace;

namespace NodeEditor.Blazor.Services;

/// <summary>
/// Extension methods for registering Node Editor services with dependency injection.
/// </summary>
public static class NodeEditorServiceExtensions
{
    /// <summary>
    /// Adds all Node Editor services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNodeEditor(this IServiceCollection services)
    {
        services.AddOptions<Plugins.PluginOptions>();
        services.AddSingleton<Plugins.IPluginServiceRegistry, Plugins.PluginServiceRegistry>();
        services.AddSingleton<Plugins.PluginLoader>();
        services.AddOptions<MarketplaceOptions>();
        services.AddSingleton<IPluginMarketplaceCache, FileBasedMarketplaceCache>();
        services.AddHttpClient<TokenBasedAuthProvider>();
        services.AddScoped<IPluginMarketplaceAuthProvider>(sp => sp.GetRequiredService<TokenBasedAuthProvider>());
        services.AddScoped<LocalPluginMarketplaceSource>();
        services.AddScoped<IPluginMarketplaceSource>(sp => sp.GetRequiredService<LocalPluginMarketplaceSource>());
        services.AddHttpClient<RemotePluginMarketplaceSource>();
        services.AddScoped<IPluginMarketplaceSource>(sp => sp.GetRequiredService<RemotePluginMarketplaceSource>());
        services.AddScoped<AggregatedPluginMarketplaceSource>();
        services.AddScoped<IPluginInstallationService, PluginInstallationService>();

        // Register state as scoped (one per user/circuit in Blazor Server, one per app in WASM)
        services.AddScoped<NodeEditorState>();
        services.AddScoped<Plugins.IPluginEventBus, Plugins.PluginEventBus>();
        
        // Register coordinate converter as scoped (tied to state)
        services.AddScoped<CoordinateConverter>();

        // Register connection validator
        services.AddScoped<ConnectionValidator>();
        
        // Register touch gesture handler as scoped
        services.AddScoped<TouchGestureHandler>();

        // Register viewport culling helper
        services.AddScoped<ViewportCuller>();
        
        // Register socket type resolver as singleton (shared type registry)
        services.AddSingleton<SocketTypeResolver>(provider =>
        {
            var resolver = new SocketTypeResolver();
            resolver.Register<ExecutionPath>();
            resolver.Register<SerializableList>();
            return resolver;
        });

        // Register node registry services
        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.NodeRegistryService>();

        // Register node context registry for plugin contexts
        services.AddSingleton<INodeContextRegistry, NodeContextRegistry>();

        // Register custom editors
        services.AddSingleton<Editors.INodeCustomEditor, Editors.DropdownEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.NumberUpDownEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.TextAreaEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ButtonEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ImageEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.TextEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.NumericEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.BoolEditorDefinition>();
        services.AddSingleton<Editors.NodeEditorCustomEditorRegistry>();

        // Register execution services
        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<BackgroundExecutionQueue>();
        services.AddScoped<BackgroundExecutionWorker>();
        services.AddScoped<NodeExecutionService>();

        // Register serialization services
        services.AddSingleton<GraphSchemaMigrator>();
        services.AddScoped<GraphSerializer>();

        // Register variable node factory (bridges graph variables to node definitions)
        services.AddScoped<VariableNodeFactory>();
        
        return services;
    }

    /// <summary>
    /// Adds all Node Editor services with a custom state factory.
    /// Useful for initializing state with pre-loaded graph data.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="stateFactory">Factory function to create the state.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddNodeEditor(
        this IServiceCollection services,
        Func<IServiceProvider, NodeEditorState> stateFactory)
    {
        services.AddOptions<Plugins.PluginOptions>();
        services.AddSingleton<Plugins.IPluginServiceRegistry, Plugins.PluginServiceRegistry>();
        services.AddSingleton<Plugins.PluginLoader>();
        services.AddOptions<MarketplaceOptions>();
        services.AddSingleton<IPluginMarketplaceCache, FileBasedMarketplaceCache>();
        services.AddHttpClient<TokenBasedAuthProvider>();
        services.AddScoped<IPluginMarketplaceAuthProvider>(sp => sp.GetRequiredService<TokenBasedAuthProvider>());
        services.AddScoped<LocalPluginMarketplaceSource>();
        services.AddScoped<IPluginMarketplaceSource>(sp => sp.GetRequiredService<LocalPluginMarketplaceSource>());
        services.AddHttpClient<RemotePluginMarketplaceSource>();
        services.AddScoped<IPluginMarketplaceSource>(sp => sp.GetRequiredService<RemotePluginMarketplaceSource>());
        services.AddScoped<AggregatedPluginMarketplaceSource>();
        services.AddScoped<IPluginInstallationService, PluginInstallationService>();

        services.AddScoped(stateFactory);
        services.AddScoped<Plugins.IPluginEventBus, Plugins.PluginEventBus>();
        services.AddScoped<CoordinateConverter>();
        services.AddScoped<ConnectionValidator>();
        services.AddScoped<ViewportCuller>();
        services.AddSingleton<SocketTypeResolver>(provider =>
        {
            var resolver = new SocketTypeResolver();
            resolver.Register<ExecutionPath>();
            resolver.Register<SerializableList>();
            return resolver;
        });

        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.NodeRegistryService>();

        services.AddSingleton<Editors.INodeCustomEditor, Editors.DropdownEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.NumberUpDownEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.TextAreaEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ButtonEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ImageEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.TextEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.NumericEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.BoolEditorDefinition>();
        services.AddSingleton<Editors.NodeEditorCustomEditorRegistry>();

        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<BackgroundExecutionQueue>();
        services.AddScoped<BackgroundExecutionWorker>();
        services.AddScoped<NodeExecutionService>();

        services.AddSingleton<GraphSchemaMigrator>();
        services.AddScoped<GraphSerializer>();

        // Register variable node factory (bridges graph variables to node definitions)
        services.AddScoped<VariableNodeFactory>();
        
        return services;
    }
}
