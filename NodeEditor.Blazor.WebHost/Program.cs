using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.WebHost.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();
builder.Services.AddNodeEditor();
builder.Services.AddScoped<NodeEditor.Blazor.Services.Execution.BackgroundExecutionWorker>();
builder.Services.Configure<PluginOptions>(builder.Configuration.GetSection(PluginOptions.SectionName));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
    var pluginLoader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
    await pluginLoader.LoadAndRegisterAsync();
}

app.Run();
