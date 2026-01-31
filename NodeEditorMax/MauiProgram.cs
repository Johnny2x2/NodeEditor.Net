using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditorMax.Services;

namespace NodeEditorMax
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            
            // Register Node Editor services
            builder.Services.AddNodeEditor();
            builder.Services.AddScoped<GraphLibraryService>();
            builder.Services.Configure<PluginOptions>(options =>
            {
                options.PluginDirectory = "plugins";
                options.ApiVersion = new Version(1, 0, 0);
            });

#if DEBUG
    		builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            using (var scope = app.Services.CreateScope())
            {
                var pluginLoader = scope.ServiceProvider.GetRequiredService<PluginLoader>();
                pluginLoader.LoadAndRegisterAsync().GetAwaiter().GetResult();
            }

            return app;
        }
    }
}
