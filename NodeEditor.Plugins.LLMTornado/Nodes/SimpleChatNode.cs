using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class SimpleChatNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Simple Chat").Category("LLMTornado/Chat")
            .Description("Send a prompt to an LLM and return text response.")
            .Callable()
            .Input<string>("Prompt", "")
            .Input<string>("System", "")
            .Input<string>("Model", "gpt-4.1-mini")
            .Input<string>("Provider", "OpenAi")
            .Input<string>("ApiKey", "")
            .Input<string>("Organization", "")
            .Input<string>("BaseUrl", "")
            .Input<double>("Temperature", 0.2)
            .Output<string>("Response")
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

            var result = await api.Chat.CreateChatCompletionSafe(request).ConfigureAwait(false);

            if (!result.Ok || result.Data is null)
            {
                context.SetOutput("Response", string.Empty);
                context.SetOutput("TotalTokens", 0);
                context.SetOutput("Ok", false);
                context.SetOutput("Error", result.Exception?.Message ?? result.Response ?? "Chat request failed.");
                await context.TriggerAsync("Exit");
                return;
            }

            var text = result.Data.Choices?.FirstOrDefault()?.Message?.Content
                       ?? result.Data.ToString()
                       ?? string.Empty;

            context.SetOutput("Response", text);
            context.SetOutput("TotalTokens", result.Data.Usage?.TotalTokens ?? 0);
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("Response", string.Empty);
            context.SetOutput("TotalTokens", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }
}