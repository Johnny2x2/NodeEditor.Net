using Microsoft.Extensions.DependencyInjection;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Plugins.LLMTornado.Services;

namespace NodeEditor.Plugins.LLMTornado.Nodes;

public sealed class ListModelsNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("List Models").Category("LLMTornado/Utility")
            .Description("List model IDs available for a provider.")
            .Callable()
            .Input<string>("Provider", "OpenAi")
            .Input<string>("ApiKey", "")
            .Input<string>("Organization", "")
            .Input<string>("BaseUrl", "")
            .Output<string[]>("ModelIds")
            .Output<int>("Count")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var providerName = context.GetInput<string>("Provider");
        var apiKey = context.GetInput<string>("ApiKey");
        var organization = context.GetInput<string>("Organization");
        var baseUrl = context.GetInput<string>("BaseUrl");

        try
        {
            var apiFactory = context.Services.GetRequiredService<ILLMTornadoApiFactory>();
            var provider = apiFactory.ResolveProvider(providerName);
            var api = apiFactory.Create(providerName, apiKey, organization, baseUrl);

            var models = await api.Models.GetModels(provider).ConfigureAwait(false) ?? [];
            var ids = models.Select(x => x.Id).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToArray();

            context.SetOutput("ModelIds", ids);
            context.SetOutput("Count", ids.Length);
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
        }
        catch (Exception ex)
        {
            context.SetOutput("ModelIds", Array.Empty<string>());
            context.SetOutput("Count", 0);
            context.SetOutput("Ok", false);
            context.SetOutput("Error", ex.Message);
        }

        await context.TriggerAsync("Exit");
    }
}