using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.WebHost.Services;

public sealed class SampleNodeContext : INodeContext
{
    [Node("Start", category: "Flow", description: "Entry point", isCallable: true, isExecutionInitiator: true)]
    public void Start(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Add", category: "Math", description: "Add two numbers", isCallable: false)]
    public void Add(int A, int B, out int Result)
    {
        Result = A + B;
    }

    [Node("Toggle", category: "Logic", description: "Negate a boolean", isCallable: false)]
    public void Toggle(bool Value, out bool Result)
    {
        Result = !Value;
    }

    [Node("Concat", category: "Text", description: "Concat strings", isCallable: false)]
    public void Concat(string A, string B, out string Result)
    {
        Result = string.Concat(A, B);
    }
}
