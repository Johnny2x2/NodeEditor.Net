using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using System.Net.Http;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class LoadImageFromUrlNode : NodeBase
{
    private static readonly HttpClient HttpClient = new();

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Load Image From URL").Category("LLMTornado/Images")
            .Description("Download an image from URL and output preview-ready image data.")
            .Callable()
            .Input<string>("ImageUrl", "")
            .Output<NodeImage?>("Image")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var imageUrl = context.GetInput<string>("ImageUrl");

        try
        {
            if (string.IsNullOrWhiteSpace(imageUrl))
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", "ImageUrl is required.");
                await context.TriggerAsync("Exit");
                return;
            }

            if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", "ImageUrl must be an absolute URL.");
                await context.TriggerAsync("Exit");
                return;
            }

            using var response = await HttpClient.GetAsync(imageUrl, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", $"Image request failed with status code {(int)response.StatusCode}.");
                await context.TriggerAsync("Exit");
                return;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
            if (bytes.Length == 0)
            {
                context.SetOutput("Image", null);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", "Downloaded image is empty.");
                await context.TriggerAsync("Exit");
                return;
            }

            var mimeType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrWhiteSpace(mimeType))
            {
                mimeType = "image/png";
            }

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
