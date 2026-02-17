using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class LoadImageNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Load Image").Category("LLMTornado/Images")
            .Description("Load an image from a local file path or data URL.")
            .Callable()
            .Input<string>("ImagePath", "")
            .Output<NodeImage?>("Image")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var imagePath = context.GetInput<string>("ImagePath");

        try
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", "ImagePath is required.");
                await context.TriggerAsync("Exit");
                return;
            }

            if (imagePath.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                context.SetOutput("Image", new NodeImage(imagePath));
                context.SetOutput("Ok", true);
                context.SetOutput("Error", string.Empty);
                await context.TriggerAsync("Exit");
                return;
            }

            if (!File.Exists(imagePath))
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", "Image file was not found.");
                await context.TriggerAsync("Exit");
                return;
            }

            var bytes = await File.ReadAllBytesAsync(imagePath, ct).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", "Image file is empty.");
                await context.TriggerAsync("Exit");
                return;
            }

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

            var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
            context.SetOutput("Image", new NodeImage(dataUrl));
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("Image", null);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }
}
