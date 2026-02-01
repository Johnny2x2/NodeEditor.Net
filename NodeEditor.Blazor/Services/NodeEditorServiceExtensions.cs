using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Serialization;

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
        services.AddSingleton<Plugins.PluginLoader>();

        // Register state as scoped (one per user/circuit in Blazor Server, one per app in WASM)
        services.AddScoped<NodeEditorState>();
        
        // Register coordinate converter as scoped (tied to state)
        services.AddScoped<CoordinateConverter>();

        // Register connection validator
        services.AddScoped<ConnectionValidator>();
        
        // Register touch gesture handler as scoped
        services.AddScoped<TouchGestureHandler>();
        
        // Register socket type resolver as singleton (shared type registry)
        services.AddSingleton<SocketTypeResolver>();

        // Register node registry services
        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.NodeRegistryService>();

        // Register custom editors
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
        services.AddSingleton<Plugins.PluginLoader>();

        services.AddScoped(stateFactory);
        services.AddScoped<CoordinateConverter>();
        services.AddScoped<ConnectionValidator>();
        services.AddSingleton<SocketTypeResolver>();

        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.NodeRegistryService>();

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
        
        return services;
    }
}
