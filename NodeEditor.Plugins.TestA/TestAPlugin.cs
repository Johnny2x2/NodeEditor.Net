using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Plugins.TestA;

public sealed class TestAPlugin : INodePlugin
{
    public string Name => "Test A Plugin";
    public string Id => "com.nodeeditormax.testa";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(TestAPlugin).Assembly);
    }
}

public sealed class TestAPluginContext : INodeContext
{
    [Node("Echo String", category: "Test", description: "Echo a string", isCallable: false)]
    public void Echo(string Input, out string Output)
    {
        Output = Input;
    }

    [Node("Ping", category: "Test", description: "Emit an execution pulse", isCallable: true, isExecutionInitiator: true)]
    public void Ping(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Load Image", category: "Test/Image", description: "Load an image from file path", isCallable: true)]
    public void LoadImage(string ImagePath, out NodeImage? Image, out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        
        if (string.IsNullOrEmpty(ImagePath))
        {
            Image = null;
            Exit.Signal();
            return;
        }

        if (ImagePath.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            Image = new NodeImage(ImagePath);
            Exit.Signal();
            return;
        }

        if (!File.Exists(ImagePath))
        {
            Image = null;
            Exit.Signal();
            return;
        }

        try
        {
            var bytes = File.ReadAllBytes(ImagePath);
            var base64 = Convert.ToBase64String(bytes);
            var ext = Path.GetExtension(ImagePath).ToLowerInvariant();
            var mimeType = ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                _ => "image/png"
            };
            
            Image = new NodeImage($"data:{mimeType};base64,{base64}");
        }
        catch
        {
            Image = null;
        }
        
        Exit.Signal();
    }
}
