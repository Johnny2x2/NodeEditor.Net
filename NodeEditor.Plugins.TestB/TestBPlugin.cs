using NodeEditor.Blazor.Services.Execution;
using NodeEditor.Blazor.Services.Plugins;
using NodeEditor.Blazor.Services.Registry;

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

public sealed class TestBPluginContext : INodeContext
{
    [Node("Add Ints", category: "Test", description: "Add two integers", isCallable: false)]
    public void Add(int A, int B, out int Result)
    {
        Result = A + B;
    }

    [Node("To Upper", category: "Test", description: "Convert string to upper-case", isCallable: false)]
    public void ToUpper(string Input, out string Output)
    {
        Output = Input.ToUpperInvariant();
    }
}
