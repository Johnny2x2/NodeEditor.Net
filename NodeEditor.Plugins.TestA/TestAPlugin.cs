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

public sealed class EchoStringNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Echo String").Category("Test")
            .Description("Echo a string")
            .Input<string>("Input", "")
            .Output<string>("Output");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Output", context.GetInput<string>("Input"));
        return Task.CompletedTask;
    }
}

public sealed class PingNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Ping").Category("Test")
            .Description("Emit an execution pulse")
            .ExecutionInitiator();
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        await context.TriggerAsync("Exit");
    }
}

public sealed class LoadImageNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Load Image").Category("Test/Image")
            .Description("Load an image from file path")
            .Callable()
            .Input<string>("ImagePath", "")
            .Output<NodeImage?>("Image");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var imagePath = context.GetInput<string>("ImagePath");
        NodeImage? image;

        if (string.IsNullOrEmpty(imagePath))
        {
            image = null;
        }
        else if (imagePath.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            image = new NodeImage(imagePath);
        }
        else if (!File.Exists(imagePath))
        {
            image = null;
        }
        else
        {
            try
            {
                var bytes = File.ReadAllBytes(imagePath);
                var base64 = Convert.ToBase64String(bytes);
                var ext = Path.GetExtension(imagePath).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };

                image = new NodeImage($"data:{mimeType};base64,{base64}");
            }
            catch
            {
                image = null;
            }
        }

        context.SetOutput("Image", image);
        await context.TriggerAsync("Exit");
    }
}
