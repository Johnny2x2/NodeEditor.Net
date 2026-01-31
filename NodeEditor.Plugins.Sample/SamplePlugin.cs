using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Plugins.Sample;

public sealed class SamplePlugin : INodePlugin
{
    public string Name => "Sample Nodes";
    public string Id => "com.nodeeditormax.sample";
    public Version Version => new(1, 0, 0);
    public Version MinApiVersion => new(1, 0, 0);

    public void Register(NodeRegistryService registry)
    {
        registry.RegisterFromAssembly(typeof(SamplePlugin).Assembly);
    }
}

public sealed class SamplePluginContext : INodeContext
{
    [Node("Multiply", category: "Math", description: "Multiply two numbers", isCallable: false)]
    public void Multiply(double A, double B, out double Result)
    {
        Result = A * B;
    }

    [Node("Clamp", category: "Math", description: "Clamp value between min/max", isCallable: false)]
    public void Clamp(double Value, double Min, double Max, out double Result)
    {
        Result = Math.Clamp(Value, Min, Max);
    }

    [Node("Random Int", category: "Math", description: "Random integer in range", isCallable: false)]
    public void RandomInt(int Min, int Max, out int Result)
    {
        Result = Random.Shared.Next(Min, Max + 1);
    }

    [Node("Pulse", category: "Flow", description: "Emit an execution pulse", isCallable: true, isExecutionInitiator: true)]
    public void Pulse(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
