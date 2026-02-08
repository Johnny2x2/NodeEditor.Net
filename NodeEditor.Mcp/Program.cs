using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Logging;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Plugins.Marketplace;
using NodeEditor.Blazor.Services.Registry;
using NodeEditor.Blazor.Services.Serialization;
using NodeEditor.Mcp.Abilities;
using NodeEditor.Mcp.Tools;

var builder = Host.CreateApplicationBuilder(args);

// Register core NodeEditor services
builder.Services.AddNodeEditor();

// Register MCP ability infrastructure
builder.Services.AddSingleton<AbilityRegistry>();
builder.Services.AddSingleton<PluginAbilityDiscovery>(sp =>
    new PluginAbilityDiscovery(
        sp.GetRequiredService<IPluginLoader>(),
        sp.GetRequiredService<AbilityRegistry>(),
        sp));

// Register MCP server with stdio transport
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new()
        {
            Name = "NodeEditorMax",
            Version = "1.0.0"
        };
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

// Create a long-lived scope for scoped services and initialize ability providers.
// Services registered as scoped (NodeEditorState, GraphSerializer, ExecutionService, etc.)
// are resolved from the scope, while singletons (PluginLoader, Logger, Registry) are resolved
// from the root provider to avoid captive dependency issues.
var scope = host.Services.CreateScope();
var sp = scope.ServiceProvider;

var abilityRegistry = host.Services.GetRequiredService<AbilityRegistry>();

// Scoped services — must come from scope
var state = sp.GetRequiredService<INodeEditorState>();
var serializer = sp.GetRequiredService<IGraphSerializer>();
var executionService = sp.GetRequiredService<INodeExecutionService>();
var headlessRunner = sp.GetRequiredService<HeadlessGraphRunner>();
var installService = sp.GetRequiredService<IPluginInstallationService>();

// Singleton services — resolved from root
var nodeRegistry = host.Services.GetRequiredService<INodeRegistryService>();
var pluginLoader = host.Services.GetRequiredService<IPluginLoader>();
var logger = host.Services.GetRequiredService<INodeEditorLogger>();

abilityRegistry.Register(new NodeAbilityProvider(state, nodeRegistry));
abilityRegistry.Register(new ConnectionAbilityProvider(state));
abilityRegistry.Register(new OverlayAbilityProvider(state));
abilityRegistry.Register(new GraphAbilityProvider(state, serializer));
abilityRegistry.Register(new CatalogAbilityProvider(nodeRegistry));
abilityRegistry.Register(new PluginAbilityProvider(pluginLoader, installService));
abilityRegistry.Register(new ExecutionAbilityProvider(state, executionService, headlessRunner, serializer));
abilityRegistry.Register(new LoggingAbilityProvider(logger));

await host.RunAsync();
