using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Adapters;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Logging;
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
        services.AddScoped<INodeEditorState, NodeEditorState>();
        return services.AddNodeEditorCore();
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
        Func<IServiceProvider, INodeEditorState> stateFactory)
    {
        services.AddScoped(stateFactory);
        return services.AddNodeEditorCore();
    }

    /// <summary>
    /// Registers all shared Node Editor services. Called by both AddNodeEditor overloads.
    /// </summary>
    private static IServiceCollection AddNodeEditorCore(this IServiceCollection services)
    {
        // State bridge (singleton — allows MCP and other singletons to reach the active circuit state)
        services.AddSingleton<INodeEditorStateBridge, NodeEditorStateBridge>();

        // Logging infrastructure (singleton — shared across Blazor circuits)
        services.AddSingleton<ILogChannelRegistry, LogChannelRegistry>();
        services.AddSingleton<INodeEditorLogger, NodeEditorLogger>();

        // Plugin infrastructure
        services.AddOptions<Plugins.PluginOptions>();
        services.AddSingleton<Plugins.IPluginServiceRegistry, Plugins.PluginServiceRegistry>();
        services.AddSingleton<Plugins.IPluginLoader, Plugins.PluginLoader>();

        // Marketplace
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

        // Event bus (bridges state events to plugins)
        services.AddScoped<Plugins.IPluginEventBus, Plugins.PluginEventBus>();

        // Coordinate converter (tied to state)
        services.AddScoped<ICoordinateConverter, CoordinateConverter>();

        // Connection validator
        services.AddScoped<IConnectionValidator, ConnectionValidator>();

        // Touch gesture handler
        services.AddScoped<ITouchGestureHandler, TouchGestureHandler>();

        // Canvas interaction handler (coordinates pointer/touch/keyboard events)
        services.AddScoped<ICanvasInteractionHandler, CanvasInteractionHandler>();

        // Viewport culler
        services.AddScoped<IViewportCuller, ViewportCuller>();

        // Socket type resolver (shared type registry)
        services.AddSingleton<ISocketTypeResolver>(provider =>
        {
            var resolver = new SocketTypeResolver();
            resolver.Register<ExecutionPath>();
            resolver.Register<SerializableList>();
            return resolver;
        });

        // Node registry & discovery
        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.INodeRegistryService, Registry.NodeRegistryService>();

        // Node context factory & registry
        services.AddSingleton<INodeContextFactory, NodeContextFactory>();
        services.AddSingleton<INodeContextRegistry, NodeContextRegistry>();

        // Adapters
        services.AddSingleton<INodeAdapter, NodeAdapter>();

        // Custom editors
        services.AddSingleton<Editors.INodeCustomEditor, Editors.DropdownEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.NumberUpDownEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.TextAreaEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ButtonEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ImageEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.TextEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.NumericEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.BoolEditorDefinition>();
        services.AddSingleton<Editors.INodeCustomEditor, Editors.ListEditorDefinition>();
        services.AddSingleton<Editors.NodeEditorCustomEditorRegistry>();

        // Execution services
        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<BackgroundExecutionQueue>();
        services.AddScoped<BackgroundExecutionWorker>();
        services.AddScoped<Execution.INodeExecutionService, Execution.NodeExecutionService>();
        services.AddScoped<Execution.HeadlessGraphRunner>();

        // Serialization services
        services.AddSingleton<GraphSchemaMigrator>();
        services.AddScoped<Serialization.IGraphSerializer, Serialization.GraphSerializer>();

        // Variable node factory (bridges graph variables to node definitions)
        services.AddScoped<VariableNodeFactory>();

        // Event node factory (bridges graph events to node definitions)
        services.AddScoped<EventNodeFactory>();

        return services;
    }
}
