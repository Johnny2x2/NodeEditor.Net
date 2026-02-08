using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Plugins.Marketplace;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.WebHost.Components;
using NodeEditor.Net.Services.Mcp;
using NodeEditor.Mcp;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});
builder.Services.AddNodeEditor();
builder.Services.AddScoped<NodeEditor.Net.Services.Execution.Runtime.BackgroundExecutionWorker>();

// Configure plugin loading
builder.Services.Configure<PluginOptions>(options =>
{
    // Plugins are installed here by the marketplace
    options.PluginDirectory = "plugins";
    options.EnablePluginLoading = true;
});

// Configure marketplace
builder.Services.Configure<MarketplaceOptions>(options =>
{
    // Local repository where plugins are published for discovery
    options.LocalRepositoryPath = Path.GetFullPath(
        Path.Combine(builder.Environment.ContentRootPath, "..", "plugin-repository"));
});

// MCP server — always registered; enabled/disabled at runtime via settings UI
builder.Services.Configure<McpOptions>(builder.Configuration.GetSection(McpOptions.SectionName));
builder.Services.AddSingleton<McpApiKeyService>();
builder.Services.AddNodeEditorMcp();

var app = builder.Build();

// Initialize MCP ability providers early so middleware is in place
var mcpOptions = app.Services.GetRequiredService<IOptions<McpOptions>>().Value;
app.Services.InitializeMcpAbilities();
app.MapNodeEditorMcp(mcpOptions.RoutePattern);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

// Skip HTTPS redirection for MCP routes — external MCP clients (Claude, Cursor, etc.)
// cannot follow 307 redirects in SSE/streaming connections, and we already gate
// access via API key middleware.
app.UseWhen(
    context => !context.Request.Path.StartsWithSegments(mcpOptions.RoutePattern),
    branch => branch.UseHttpsRedirection());

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var pluginLoader = scope.ServiceProvider.GetRequiredService<IPluginLoader>();
    await pluginLoader.LoadAndRegisterAsync();
}

app.Run();
