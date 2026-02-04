using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Plugins.TestA;

public sealed class TestAPlugin : INodePlugin
{
    public string Name => "Test A Plugin";
    public string Id => "com.nodeeditormax.testa";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(TestAPlugin).Assembly);
    }
}

public sealed class TestAPluginContext : INodeContext
{
    [Node("Echo String", category: "Test", description: "Echo a string", isCallable: false)]
    public void Echo(string Input, out string Output)
    {
        Output = Input;
    }

    [Node("Ping", category: "Test", description: "Emit an execution pulse", isCallable: true, isExecutionInitiator: true)]
    public void Ping(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
