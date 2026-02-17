using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Images;
using LlmTornado.Images.Models;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class StreamResponseNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Stream Response").Category("LLMTornado/Responses")
            .Description("Stream output text deltas from the Responses API.")
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
            .StreamOutput<string>("Delta", "OnDelta", "Completed")
            .StreamOutput<NodeImage?>("ImageDelta", "OnImage", "Completed")
            .Output<string>("FinalText")
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

        var finalText = new StringBuilder();
        NodeImage? finalImage = null;
        string finalImageReference = string.Empty;
        var totalTokens = 0;
        string? error = null;

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
                CancellationToken = ct,
                Stream = true
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

            ValueTask OnDelta(ResponseEventOutputTextDelta evt)
            {
                if (string.IsNullOrEmpty(evt.Delta))
                {
                    return ValueTask.CompletedTask;
                }

                finalText.Append(evt.Delta);
                return new ValueTask(context.EmitAsync("Delta", evt.Delta));
            }

            ValueTask OnCompleted(ResponseEventCompleted evt)
            {
                if (!string.IsNullOrWhiteSpace(evt.Response?.OutputText))
                {
                    finalText.Clear();
                    finalText.Append(evt.Response.OutputText);
                }

                var imageBase64 = evt.Response?.Output
                    ?.OfType<ResponseImageGenToolCallItem>()
                    .Select(x => x.Result)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

                if (!string.IsNullOrWhiteSpace(imageBase64))
                {
                    finalImageReference = $"data:image/png;base64,{imageBase64}";
                    finalImage = new NodeImage(finalImageReference);
                }

                totalTokens = evt.Response?.Usage?.TotalTokens ?? totalTokens;
                return ValueTask.CompletedTask;
            }

            ValueTask OnEvent(IResponseEvent evt)
            {
                if (evt is ResponseEventImageGenerationCallPartialImage partialImage && !string.IsNullOrWhiteSpace(partialImage.PartialImageB64))
                {
                    var partialDataUrl = $"data:image/png;base64,{partialImage.PartialImageB64}";
                    return new ValueTask(context.EmitAsync("ImageDelta", new NodeImage(partialDataUrl)));
                }

                return ValueTask.CompletedTask;
            }

            ValueTask OnError(ResponseEventError evt)
            {
                error = evt.Message;
                return ValueTask.CompletedTask;
            }

            var handler = new ResponseStreamEventHandler
            {
                OnResponseOutputTextDelta = OnDelta,
                OnResponseCompleted = OnCompleted,
                OnResponseError = OnError,
                OnEvent = OnEvent,
                OnException = req =>
                {
                    error = req.Exception?.Message ?? req.Response?.ReasonPhrase ?? "Streaming request failed.";
                    return ValueTask.CompletedTask;
                }
            };

            await api.Responses.StreamResponseRichSafe(request, handler, ct).ConfigureAwait(false);

            context.SetOutput("FinalText", finalText.ToString());
            context.SetOutput("GeneratedImage", finalImage);
            context.SetOutput("GeneratedImageReference", finalImageReference);
            context.SetOutput("TotalTokens", totalTokens);
            context.SetOutput("Ok", string.IsNullOrWhiteSpace(error));
            context.SetOutput("Error", error ?? string.Empty);

            if (string.IsNullOrWhiteSpace(error))
            {
                await context.TriggerAsync("Completed");
            }
            else
            {
                await context.TriggerAsync("Exit");
            }
        }
        catch (Exception ex)
        {
            context.SetOutput("FinalText", finalText.ToString());
            context.SetOutput("GeneratedImage", finalImage);
            context.SetOutput("GeneratedImageReference", finalImageReference);
            context.SetOutput("TotalTokens", totalTokens);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
            await context.TriggerAsync("Exit");
        }
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