using LlmTornado.Embedding.Models;
using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class CreateEmbeddingNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Create Embedding").Category("LLMTornado/Embeddings")
            .Description("Generate a vector embedding for text.")
            .Callable()
            .Input<string>("Text", "")
            .Input<string>("Model", "text-embedding-3-small")
            .Input<string>("Provider", "OpenAi")
            .Input<string>("ApiKey", "")
            .Input<string>("Organization", "")
            .Input<string>("BaseUrl", "")
            .Output<float[]>("Vector")
            .Output<int>("Dimensions")
            .Output<int>("TotalTokens")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var text = context.GetInput<string>("Text");
        var modelName = context.GetInput<string>("Model");
        var providerName = context.GetInput<string>("Provider");
        var apiKey = context.GetInput<string>("ApiKey");
        var organization = context.GetInput<string>("Organization");
        var baseUrl = context.GetInput<string>("BaseUrl");

        try
        {
            var apiFactory = context.Services.GetRequiredService<ILLMTornadoApiFactory>();
            var provider = apiFactory.ResolveProvider(providerName);
            var api = apiFactory.Create(providerName, apiKey, organization, baseUrl);

            var model = string.IsNullOrWhiteSpace(modelName)
                ? new EmbeddingModel("text-embedding-3-small", provider)
                : new EmbeddingModel(modelName, provider);

            var result = await api.Embeddings.CreateEmbedding(model, text).ConfigureAwait(false);
            var vector = result?.Data?.FirstOrDefault()?.Embedding ?? [];

            context.SetOutput("Vector", vector);
            context.SetOutput("Dimensions", vector.Length);
            context.SetOutput("TotalTokens", result?.Usage?.TotalTokens ?? 0);
            context.SetOutput("Ok", result is not null);
            context.SetOutput("Error", result is null ? "Embedding request returned no data." : string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("Vector", Array.Empty<float>());
            context.SetOutput("Dimensions", 0);
            context.SetOutput("TotalTokens", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }
}