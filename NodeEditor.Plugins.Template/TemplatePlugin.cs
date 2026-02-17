using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Plugins.Template;

public sealed class TemplatePlugin : INodePlugin
{
    public string Name => "Template Plugin";
    public string Id => "com.nodeeditormax.template";
    public Version Version => new(0, 1, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(TemplatePlugin).Assembly);
    }
}

public sealed class EchoNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Echo").Category("SDK")
            .Description("Pass input to output")
            .Input<string>("Input", "")
            .Output<string>("Output");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Output", context.GetInput<string>("Input"));
        return Task.CompletedTask;
    }
}
