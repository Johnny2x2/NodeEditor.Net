using Microsoft.Extensions.Logging;
using BasicNodeEditor.Contexts;
using NodeEditor.Blazor;

namespace BasicNodeEditor;

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

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Services.AddLogging(logging =>
        {
            logging.AddDebug();
        });
#endif

        // Register NodeEditor.Blazor with sample node contexts
        builder.Services.AddNodeEditor(config =>
        {
            // Register node contexts
            config.RegisterNodeContext<MathNodeContext>();
            config.RegisterNodeContext<LogicNodeContext>();
            config.RegisterNodeContext<StringNodeContext>();

            // Enable performance optimizations
            config.EnableViewportCulling = true;

            // Configure socket type resolver
            config.ConfigureSocketTypeResolver(resolver =>
            {
                resolver.RegisterType<int>("Number");
                resolver.RegisterType<double>("Number");
                resolver.RegisterType<float>("Number");
                resolver.RegisterType<string>("Text");
                resolver.RegisterType<bool>("Boolean");
            });
        });

        return builder.Build();
    }
}
