using System.Text;
using LlmTornado.Chat.Models;
using LlmTornado.Responses;
using LlmTornado.Responses.Events;
using Microsoft.Extensions.DependencyInjection;
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
            .Input<double>("Temperature", 0.2)
            .StreamOutput<string>("Delta", "OnDelta", "Completed")
            .Output<string>("FinalText")
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
        var temperature = context.GetInput<double>("Temperature");

        var finalText = new StringBuilder();
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
                InputString = prompt,
                Instructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions,
                Temperature = temperature,
                CancellationToken = ct,
                Stream = true
            };

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

                totalTokens = evt.Response?.Usage?.TotalTokens ?? totalTokens;
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
                OnException = req =>
                {
                    error = req.Exception?.Message ?? req.Response?.ReasonPhrase ?? "Streaming request failed.";
                    return ValueTask.CompletedTask;
                }
            };

            await api.Responses.StreamResponseRichSafe(request, handler, ct).ConfigureAwait(false);

            context.SetOutput("FinalText", finalText.ToString());
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
            context.SetOutput("TotalTokens", totalTokens);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
            await context.TriggerAsync("Exit");
        }
    }
}