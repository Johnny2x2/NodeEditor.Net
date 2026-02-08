using LlmTornado;
using LlmTornado.Embedding;
using LlmTornado.Embedding.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;
using NodeEditor.Plugins.LLMTornado.Configuration;

namespace NodeEditor.Plugins.LLMTornado.Contexts;

public sealed class EmbeddingContext : INodeContext
{
    [Node("Create Embedding",
        category: "LLM/Embeddings",
        description: "Convert text to a vector embedding",
        isCallable: true)]
    public void CreateEmbedding(
        string Text,
        string? Model,
        out float[] Vector,
        out int TokensUsed,
        out string Error,
        out ExecutionPath Next)
    {
        Next = new ExecutionPath();
        Vector = Array.Empty<float>();
        TokensUsed = 0;
        Error = string.Empty;

        try
        {
            var config = LLMTornadoConfiguration.FromEnvironment();
            var api = config.CreateApi();

            var embeddingModel = !string.IsNullOrWhiteSpace(Model)
                ? (EmbeddingModel)Model
                : EmbeddingModel.OpenAi.Gen3.Small;

            var result = api.Embeddings.CreateEmbedding(embeddingModel, Text ?? string.Empty)
                .GetAwaiter().GetResult();

            if (result?.Data is { Count: > 0 })
            {
                Vector = result.Data[0].Embedding ?? Array.Empty<float>();
            }

            if (result?.Usage is not null)
            {
                TokensUsed = result.Usage.TotalTokens;
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Error = ex.InnerException?.Message ?? ex.Message;
        }

        Next.Signal();
    }

    [Node("Cosine Similarity",
        category: "LLM/Embeddings",
        description: "Compute cosine similarity between two embedding vectors",
        isCallable: false)]
    public void CosineSimilarity(
        float[] VectorA,
        float[] VectorB,
        out double Similarity)
    {
        Similarity = 0.0;

        if (VectorA is null || VectorB is null || VectorA.Length == 0 || VectorB.Length == 0)
            return;

        if (VectorA.Length != VectorB.Length)
            return;

        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;
        for (int i = 0; i < VectorA.Length; i++)
        {
            dotProduct += VectorA[i] * (double)VectorB[i];
            magnitudeA += VectorA[i] * (double)VectorA[i];
            magnitudeB += VectorB[i] * (double)VectorB[i];
        }

        var denominator = Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB);
        if (denominator > 0)
        {
            Similarity = dotProduct / denominator;
        }
    }
}
