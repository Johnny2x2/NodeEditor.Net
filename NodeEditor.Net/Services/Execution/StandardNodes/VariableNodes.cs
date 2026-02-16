namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Get/Set Variable nodes for reading and writing named variables
/// within the execution context. Variables persist across the entire
/// execution run and can be used to pass data between unconnected 
/// parts of the graph.
/// </summary>
public sealed class GetVariableNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Get Variable").Category("Variables")
            .Description("Reads a named variable from the execution context.")
            .Input<string>("Name", "myVar")
            .Output<object>("Value");
    }

    public override Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var name = context.GetInput<string>("Name") ?? "myVar";
        context.SetOutput("Value", context.GetVariable(name));
        return Task.CompletedTask;
    }
}

public sealed class SetVariableNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Set Variable").Category("Variables")
            .Description("Writes a named variable to the execution context.")
            .Callable()
            .Input<string>("Name", "myVar")
            .Input<object>("Value")
            .Output<object>("Value");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var name = context.GetInput<string>("Name") ?? "myVar";
        var value = context.GetInput<object>("Value");
        context.SetVariable(name, value);
        context.SetOutput("Value", value);
        await context.TriggerAsync("Exit");
    }
}

public sealed class IncrementVariableNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Increment Variable").Category("Variables")
            .Description("Increments a named integer variable by a step and outputs the new value.")
            .Callable()
            .Input<string>("Name", "counter")
            .Input<int>("Step", 1)
            .Output<int>("Value");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var name = context.GetInput<string>("Name") ?? "counter";
        var step = context.GetInput<int>("Step");
        var current = context.GetVariable(name);
        var currentInt = current switch
        {
            int i => i,
            double d => (int)d,
            _ => 0
        };
        var newValue = currentInt + step;
        context.SetVariable(name, newValue);
        context.SetOutput("Value", newValue);
        await context.TriggerAsync("Exit");
    }
}
