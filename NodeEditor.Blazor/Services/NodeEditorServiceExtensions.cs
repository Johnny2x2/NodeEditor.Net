using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Blazor.Services.Execution;

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
        // Register state as scoped (one per user/circuit in Blazor Server, one per app in WASM)
        services.AddScoped<NodeEditorState>();
        
        // Register coordinate converter as scoped (tied to state)
        services.AddScoped<CoordinateConverter>();

        // Register connection validator
        services.AddScoped<ConnectionValidator>();
        
        // Register socket type resolver as singleton (shared type registry)
        services.AddSingleton<SocketTypeResolver>();

        // Register node registry services
        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.NodeRegistryService>();

        // Register execution services
        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<BackgroundExecutionQueue>();
        services.AddSingleton<BackgroundExecutionWorker>();
        services.AddScoped<NodeExecutionService>();
        
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
        services.AddScoped(stateFactory);
        services.AddScoped<CoordinateConverter>();
        services.AddScoped<ConnectionValidator>();
        services.AddSingleton<SocketTypeResolver>();

        services.AddSingleton<Registry.NodeDiscoveryService>();
        services.AddSingleton<Registry.NodeRegistryService>();

        services.AddSingleton<ExecutionPlanner>();
        services.AddSingleton<BackgroundExecutionQueue>();
        services.AddSingleton<BackgroundExecutionWorker>();
        services.AddScoped<NodeExecutionService>();
        
        return services;
    }
}
