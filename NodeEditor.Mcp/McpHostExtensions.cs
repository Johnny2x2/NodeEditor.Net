using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Logging;
using NodeEditor.Net.Services.Mcp;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Plugins.Marketplace;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Net.Services.Serialization;
using NodeEditor.Mcp.Abilities;
using NodeEditor.Mcp.Tools;

namespace NodeEditor.Mcp;

/// <summary>
/// Extension methods for embedding the MCP server into an ASP.NET Core host.
/// </summary>
public static class McpHostExtensions
{
    /// <summary>
    /// Registers MCP server services, the ability infrastructure, and the HTTP transport.
    /// The server is always registered; runtime enable/disable is handled by the middleware.
    /// </summary>
    public static IServiceCollection AddNodeEditorMcp(this IServiceCollection services)
    {
        // MCP infrastructure
        services.AddSingleton<AbilityRegistry>();
        services.AddSingleton<PluginAbilityDiscovery>(sp =>
            new PluginAbilityDiscovery(
                sp.GetRequiredService<IPluginLoader>(),
                sp.GetRequiredService<AbilityRegistry>(),
                sp));

        // MCP server with HTTP (Streamable HTTP + legacy SSE) transport
        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "NodeEditor",
                    Version = "1.0.0"
                };
            })
            .WithHttpTransport()
            .WithToolsFromAssembly(typeof(NodeEditorTools).Assembly);

        return services;
    }

    /// <summary>
    /// Initializes the ability providers using the state bridge and registers
    /// them with the <see cref="AbilityRegistry"/>. Call after <c>app.Build()</c>.
    /// </summary>
    public static void InitializeMcpAbilities(this IServiceProvider services)
    {
        var registry = services.GetRequiredService<AbilityRegistry>();
        var bridge = services.GetRequiredService<INodeEditorStateBridge>();

        // Create a BridgedNodeEditorState that dynamically delegates to the
        // active Blazor circuit's state. Ability providers hold this reference
        // and it resolves correctly as circuits attach/detach.
        var bridgedState = new BridgedNodeEditorState(bridge);

        // Scoped services need a scope — create one that lives for the app lifetime.
        // This scope provides serializer, execution, headless runner, etc.
        var scope = services.CreateScope();
        var sp = scope.ServiceProvider;

        var serializer = sp.GetRequiredService<IGraphSerializer>();
        var executionService = sp.GetRequiredService<INodeExecutionService>();
        var headlessRunner = sp.GetRequiredService<HeadlessGraphRunner>();
        var installService = sp.GetRequiredService<IPluginInstallationService>();

        // Singletons from root
        var nodeRegistry = services.GetRequiredService<INodeRegistryService>();
        var pluginLoader = services.GetRequiredService<IPluginLoader>();
        var logger = services.GetRequiredService<INodeEditorLogger>();

        registry.Register(new NodeAbilityProvider(bridgedState, nodeRegistry));
        registry.Register(new ConnectionAbilityProvider(bridgedState));
        registry.Register(new OverlayAbilityProvider(bridgedState));
        registry.Register(new GraphAbilityProvider(bridgedState, serializer));
        registry.Register(new CatalogAbilityProvider(nodeRegistry));
        registry.Register(new PluginAbilityProvider(pluginLoader, installService));
        registry.Register(new ExecutionAbilityProvider(bridgedState, executionService, headlessRunner, serializer));
        registry.Register(new LoggingAbilityProvider(logger));
    }

    /// <summary>
    /// Maps the MCP endpoints and adds the API-key validation middleware.
    /// </summary>
    public static IEndpointConventionBuilder MapNodeEditorMcp(
        this WebApplication app,
        string routePattern = "/mcp")
    {
        // Insert API key middleware before MCP endpoints
        app.UseMiddleware<McpApiKeyMiddleware>(
            app.Services.GetRequiredService<McpApiKeyService>(),
            app.Services.GetRequiredService<INodeEditorLogger>(),
            routePattern);

        // Log that the MCP endpoint is mapped
        var logger = app.Services.GetRequiredService<INodeEditorLogger>();
        logger.Log(LogChannels.Mcp, LogLevel.Info,
            $"MCP endpoint mapped at {routePattern}");

        // Disable antiforgery on MCP routes — external clients don't carry tokens
        return app.MapMcp(routePattern).DisableAntiforgery();
    }
}
