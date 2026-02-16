using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardConstantNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("Number")
            .Category("Constants").Description("A constant number value.")
            .Input<double>("Value", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("Value")))
            .Build();

        yield return NodeBuilder.Create("Integer")
            .Category("Constants").Description("A constant integer value.")
            .Input<int>("Value", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value")))
            .Build();

        yield return NodeBuilder.Create("String")
            .Category("Constants").Description("A constant string value.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<string>("Value") ?? ""))
            .Build();

        yield return NodeBuilder.Create("Boolean")
            .Category("Constants").Description("A constant boolean value.")
            .Input<bool>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("Value")))
            .Build();

        // ── Pass through / identity ──

        yield return NodeBuilder.Create("Pass Through")
            .Category("Constants").Description("Passes a value through unchanged. Useful for organizing wires.")
            .Input<object>("Value").Output<object>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<object>("Value")))
            .Build();

        // ── Null ──

        yield return NodeBuilder.Create("Null")
            .Category("Constants").Description("Outputs a null value.")
            .Output<object>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput<object>("Result", null!))
            .Build();
    }
}
