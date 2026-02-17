using LlmTornado.Chat.Models;
using LlmTornado.Images;
using LlmTornado.Images.Models;
using LlmTornado.Responses;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class CreateResponseNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Create Response").Category("LLMTornado/Responses")
            .Description("Create a response using the OpenAI-compatible Responses API.")
            .Callable()
            .Input<string>("Prompt", "")
            .Input<string>("Instructions", "")
            .Input<string>("Model", "gpt-4.1-mini")
            .Input<string>("Provider", "OpenAi")
            .Input<string>("ApiKey", "")
            .Input<string>("Organization", "")
            .Input<string>("BaseUrl", "")
            .Input<NodeImage?>("Image", null)
            .Input<string>("ImageUrl", "")
            .Input<string>("ImageDetail", "Auto")
            .Input<bool>("EnableImageGeneration", false)
            .Input<string>("ImageGenerationModel", "gpt-image-1")
            .Input<double>("Temperature", 0.2)
            .Output<string>("ResponseId")
            .Output<string>("OutputText")
            .Output<string>("Status")
            .Output<NodeImage?>("GeneratedImage")
            .Output<string>("GeneratedImageReference")
            .Output<int>("TotalTokens")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var prompt = context.GetInput<string>("Prompt");
        var instructions = context.GetInput<string>("Instructions");
        var modelName = context.GetInput<string>("Model");
        var providerName = context.GetInput<string>("Provider");
        var apiKey = context.GetInput<string>("ApiKey");
        var organization = context.GetInput<string>("Organization");
        var baseUrl = context.GetInput<string>("BaseUrl");
        var image = context.GetInput<NodeImage?>("Image");
        var imageUrl = context.GetInput<string>("ImageUrl");
        var imageDetail = ParseImageDetail(context.GetInput<string>("ImageDetail"));
        var enableImageGeneration = context.GetInput<bool>("EnableImageGeneration");
        var imageGenerationModel = context.GetInput<string>("ImageGenerationModel");
        var temperature = context.GetInput<double>("Temperature");

        try
        {
            var apiFactory = context.Services.GetRequiredService<ILLMTornadoApiFactory>();
            var provider = apiFactory.ResolveProvider(providerName);
            var api = apiFactory.Create(providerName, apiKey, organization, baseUrl);

            var request = new ResponseRequest
            {
                Model = string.IsNullOrWhiteSpace(modelName)
                    ? new ChatModel("gpt-4.1-mini", provider)
                    : new ChatModel(modelName, provider),
                Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions,
                Temperature = temperature,
                CancellationToken = ct
            };

            var inputItems = BuildInputItems(prompt, image, imageUrl, imageDetail);
            if (inputItems.Count == 0)
            {
                request.InputString = prompt;
            }
            else
            {
                request.InputItems = inputItems;
            }

            if (enableImageGeneration)
            {
                request.Tools =
                [
                    new ResponseImageGenerationTool
                    {
                        Model = string.IsNullOrWhiteSpace(imageGenerationModel)
                            ? new ImageModel("gpt-image-1", provider)
                            : new ImageModel(imageGenerationModel, provider)
                    }
                ];
            }

            var result = await api.Responses.CreateResponseSafe(request).ConfigureAwait(false);

            if (!result.Ok || result.Data is null)
            {
                context.SetOutput("ResponseId", string.Empty);
                context.SetOutput("OutputText", string.Empty);
                context.SetOutput("Status", "Failed");
                context.SetOutput("TotalTokens", 0);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", result.Exception?.Message ?? result.Response ?? "Response request failed.");
                await context.TriggerAsync("Exit");
                return;
            }

            context.SetOutput("ResponseId", result.Data.Id);
            context.SetOutput("OutputText", result.Data.OutputText ?? string.Empty);
            context.SetOutput("Status", result.Data.Status.ToString());
            var outputItems = result.Data.Output;
            var generatedImageBase64 = outputItems is null
                ? null
                : outputItems
                    .OfType<ResponseImageGenToolCallItem>()
                    .Select(x => x.Result)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

            var generatedImageReference = string.IsNullOrWhiteSpace(generatedImageBase64)
                ? string.Empty
                : $"data:image/png;base64,{generatedImageBase64}";

            context.SetOutput("GeneratedImage", string.IsNullOrWhiteSpace(generatedImageReference) ? null : new NodeImage(generatedImageReference));
            context.SetOutput("GeneratedImageReference", generatedImageReference);
            context.SetOutput("TotalTokens", result.Data.Usage?.TotalTokens ?? 0);
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("ResponseId", string.Empty);
            context.SetOutput("OutputText", string.Empty);
            context.SetOutput("Status", "Failed");
            context.SetOutput("GeneratedImage", null);
            context.SetOutput("GeneratedImageReference", string.Empty);
            context.SetOutput("TotalTokens", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }

    private static List<ResponseInputItem> BuildInputItems(string prompt, NodeImage? image, string? imageUrl, ImageDetail detail)
    {
        var content = new List<ResponseInputContent>();

        if (!string.IsNullOrWhiteSpace(prompt))
        {
            content.Add(new ResponseInputContentText(prompt));
        }

        var imageContent = !string.IsNullOrWhiteSpace(image?.DataUrl)
            ? image!.DataUrl
            : imageUrl;

        if (!string.IsNullOrWhiteSpace(imageContent))
        {
            content.Add(new ResponseInputContentImage
            {
                ImageUrl = imageContent,
                Detail = detail
            });
        }

        if (content.Count == 0)
        {
            return [];
        }

        return [new ResponseInputMessage { Content = content }];
    }

    private static ImageDetail ParseImageDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return ImageDetail.Auto;
        }

        return detail.Trim().ToLowerInvariant() switch
        {
            "low" => ImageDetail.Low,
            "high" => ImageDetail.High,
            _ => ImageDetail.Auto
        };
    }
}