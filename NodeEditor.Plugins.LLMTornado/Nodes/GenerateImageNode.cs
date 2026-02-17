using LlmTornado.Images;
using LlmTornado.Images.Models;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;
using System.Net.Http;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class GenerateImageNode : NodeBase
{
    private static readonly HttpClient HttpClient = new();

    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Generate Image").Category("LLMTornado/Images")
            .Description("Generate an image with LLMTornado and output preview-ready image data.")
            .Callable()
            .Input<string>("Prompt", "A colorful abstract shape")
            .Input<string>("Model", "gpt-image-1")
            .Input<string>("Provider", "OpenAi")
            .Input<string>("Size", "1024x1024")
            .Input<int>("Count", 1)
            .Input<string>("ResponseFormat", "Base64")
            .Output<NodeImage?>("Image")
            .Output<string>("ImageReference")
            .Output<int>("TotalTokens")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var prompt = context.GetInput<string>("Prompt");
        var modelName = context.GetInput<string>("Model");
        var providerName = context.GetInput<string>("Provider");
        var sizeText = context.GetInput<string>("Size");
        var count = context.GetInput<int>("Count");
        var responseFormatText = context.GetInput<string>("ResponseFormat");

        if (string.IsNullOrWhiteSpace(prompt))
        {
            context.SetOutput("Image", null);
            context.SetOutput("ImageReference", string.Empty);
            context.SetOutput("TotalTokens", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", "Prompt is required.");
            await context.TriggerAsync("Exit");
            return;
        }

        try
        {
            var apiFactory = context.Services.GetRequiredService<ILLMTornadoApiFactory>();
            var provider = apiFactory.ResolveProvider(providerName);
            var api = apiFactory.Create(providerOverride: providerName);

            var request = new ImageGenerationRequest(prompt)
            {
                Model = string.IsNullOrWhiteSpace(modelName)
                    ? new ImageModel("gpt-image-1", provider)
                    : new ImageModel(modelName, provider),
                NumOfImages = Math.Max(1, count),
                ResponseFormat = ParseResponseFormat(responseFormatText)
            };

            ApplySize(request, sizeText);

            var result = await api.ImageGenerations.CreateImage(request).ConfigureAwait(false);
            var generated = result?.Data?.FirstOrDefault();

            NodeImage? image = null;
            string imageReference = string.Empty;

            if (generated is not null)
            {
                if (!string.IsNullOrWhiteSpace(generated.Base64))
                {
                    var dataUrl = $"data:image/png;base64,{generated.Base64}";
                    image = new NodeImage(dataUrl);
                    imageReference = dataUrl;
                }
                else if (!string.IsNullOrWhiteSpace(generated.Url))
                {
                    imageReference = generated.Url;
                    image = await DownloadAsNodeImageAsync(generated.Url, ct).ConfigureAwait(false);
                }
            }

            context.SetOutput("Image", image);
            context.SetOutput("ImageReference", imageReference);
            context.SetOutput("TotalTokens", result?.Usage?.TotalTokens ?? 0);
            context.SetOutput("Ok", image is not null || !string.IsNullOrWhiteSpace(imageReference));
            context.SetOutput("Error", image is null && string.IsNullOrWhiteSpace(imageReference) ? "Image request returned no data." : string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("Image", null);
            context.SetOutput("ImageReference", string.Empty);
            context.SetOutput("TotalTokens", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }

    private static TornadoImageResponseFormats? ParseResponseFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TornadoImageResponseFormats.Base64;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "url" => TornadoImageResponseFormats.Url,
            _ => TornadoImageResponseFormats.Base64
        };
    }

    private static void ApplySize(ImageGenerationRequest request, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var size = value.Trim().ToLowerInvariant();
        switch (size)
        {
            case "256x256":
                request.Size = TornadoImageSizes.Size256x256;
                return;
            case "512x512":
                request.Size = TornadoImageSizes.Size512x512;
                return;
            case "1024x1024":
                request.Size = TornadoImageSizes.Size1024x1024;
                return;
            case "1024x1536":
                request.Size = TornadoImageSizes.Size1024x1536;
                return;
            case "1536x1024":
                request.Size = TornadoImageSizes.Size1536x1024;
                return;
            case "auto":
                request.Size = TornadoImageSizes.Auto;
                return;
        }

        var parts = size.Split('x', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && int.TryParse(parts[0], out var width) && int.TryParse(parts[1], out var height) && width > 0 && height > 0)
        {
            request.Width = width;
            request.Height = height;
        }
    }

    private static async Task<NodeImage?> DownloadAsNodeImageAsync(string imageUrl, CancellationToken ct)
    {
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out _))
        {
            return null;
        }

        using var response = await HttpClient.GetAsync(imageUrl, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        if (bytes.Length == 0)
        {
            return null;
        }

        var mimeType = response.Content.Headers.ContentType?.MediaType;
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            mimeType = "image/png";
        }

        var dataUrl = $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
        return new NodeImage(dataUrl);
    }
}