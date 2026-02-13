using LlmTornado.Chat.Models;
using LlmTornado.Responses;
using Microsoft.Extensions.DependencyInjection;
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
            .Input<double>("Temperature", 0.2)
            .Output<string>("ResponseId")
            .Output<string>("OutputText")
            .Output<string>("Status")
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
                CancellationToken = ct
            };

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
            context.SetOutput("TotalTokens", result.Data.Usage?.TotalTokens ?? 0);
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("ResponseId", string.Empty);
            context.SetOutput("OutputText", string.Empty);
            context.SetOutput("Status", "Failed");
            context.SetOutput("TotalTokens", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }
}