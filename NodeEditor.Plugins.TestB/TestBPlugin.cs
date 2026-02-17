using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Plugins.TestB;

public sealed class TestBPlugin : INodePlugin
{
    public string Name => "Test B Plugin";
    public string Id => "com.nodeeditormax.testb";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(INodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(TestBPlugin).Assembly);
    }
}

public sealed class AddIntsNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add Ints").Category("Test")
            .Description("Add two integers")
            .Input<int>("A", 0)
            .Input<int>("B", 0)
            .Output<int>("Result");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Result", context.GetInput<int>("A") + context.GetInput<int>("B"));
        return Task.CompletedTask;
    }
}

public sealed class ToUpperNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("To Upper").Category("Test")
            .Description("Convert string to upper-case")
            .Input<string>("Input", "")
            .Output<string>("Output");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        context.SetOutput("Output", context.GetInput<string>("Input").ToUpperInvariant());
        return Task.CompletedTask;
    }
}
