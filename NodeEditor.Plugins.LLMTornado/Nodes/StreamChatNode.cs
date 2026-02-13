using System.Text;
using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class StreamChatNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Stream Chat").Category("LLMTornado/Chat")
            .Description("Stream chat output token-by-token.")
            .Callable()
            .Input<string>("Prompt", "")
            .Input<string>("System", "")
            .Input<string>("Model", "gpt-4.1-mini")
            .Input<string>("Provider", "OpenAi")
            .Input<string>("ApiKey", "")
            .Input<string>("Organization", "")
            .Input<string>("BaseUrl", "")
            .Input<double>("Temperature", 0.2)
            .StreamOutput<string>("Token", "OnToken", "Completed")
            .Output<string>("FinalResponse")
            .Output<int>("TotalTokens")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var prompt = context.GetInput<string>("Prompt");
        var system = context.GetInput<string>("System");
        var modelName = context.GetInput<string>("Model");
        var providerName = context.GetInput<string>("Provider");
        var apiKey = context.GetInput<string>("ApiKey");
        var organization = context.GetInput<string>("Organization");
        var baseUrl = context.GetInput<string>("BaseUrl");
        var temperature = context.GetInput<double>("Temperature");

        var combined = new StringBuilder();
        var totalTokens = 0;

        try
        {
            var apiFactory = context.Services.GetRequiredService<ILLMTornadoApiFactory>();
            var provider = apiFactory.ResolveProvider(providerName);
            var api = apiFactory.Create(providerName, apiKey, organization, baseUrl);

            var request = new ChatRequest
            {
                Model = string.IsNullOrWhiteSpace(modelName)
                    ? new ChatModel("gpt-4.1-mini", provider)
                    : new ChatModel(modelName, provider),
                Temperature = temperature,
                CancellationToken = ct,
                Messages = []
            };

            if (!string.IsNullOrWhiteSpace(system))
            {
                request.Messages.Add(new ChatMessage(ChatMessageRoles.System, system));
            }

            request.Messages.Add(new ChatMessage(ChatMessageRoles.User, prompt));

            await foreach (var chunk in api.Chat.StreamChatEnumerable(request).WithCancellation(ct).ConfigureAwait(false))
            {
                totalTokens = chunk.Usage?.TotalTokens ?? totalTokens;

                var token = chunk.Choices?.FirstOrDefault()?.Delta?.Content;
                if (!string.IsNullOrEmpty(token))
                {
                    var append = token;
                    var current = combined.ToString();

                    if (append.StartsWith(current, StringComparison.Ordinal))
                    {
                        append = append[current.Length..];
                    }

                    if (append.Length == 0)
                    {
                        continue;
                    }

                    combined.Append(append);
                    await context.EmitAsync("Token", append);
                }
            }

            context.SetOutput("FinalResponse", combined.ToString());
            context.SetOutput("TotalTokens", totalTokens);
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
            await context.TriggerAsync("Completed");
        }
        catch (Exception ex)
        {
            context.SetOutput("FinalResponse", combined.ToString());
            context.SetOutput("TotalTokens", totalTokens);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
            await context.TriggerAsync("Exit");
        }
    }
}