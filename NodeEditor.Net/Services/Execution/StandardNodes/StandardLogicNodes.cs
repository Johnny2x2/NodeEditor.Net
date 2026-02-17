using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardLogicNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        // ── Comparison (double) ──

        yield return NodeBuilder.Create("Equal")
            .Category("Logic").Description("True if A equals B.")
            .Input<object>("A").Input<object>("B").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Equals(ctx.GetInput<object>("A"), ctx.GetInput<object>("B"))))
            .Build();

        yield return NodeBuilder.Create("Not Equal")
            .Category("Logic").Description("True if A does not equal B.")
            .Input<object>("A").Input<object>("B").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", !Equals(ctx.GetInput<object>("A"), ctx.GetInput<object>("B"))))
            .Build();

        yield return NodeBuilder.Create("Greater Than")
            .Category("Logic").Description("True if A > B.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") > ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Less Than")
            .Category("Logic").Description("True if A < B.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") < ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Greater Or Equal")
            .Category("Logic").Description("True if A ≥ B.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") >= ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Less Or Equal")
            .Category("Logic").Description("True if A ≤ B.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") <= ctx.GetInput<double>("B")))
            .Build();

        // ── Integer comparison ──

        yield return NodeBuilder.Create("Equal Int")
            .Category("Logic").Description("True if A equals B (integers).")
            .Input<int>("A", 0).Input<int>("B", 0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") == ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Greater Than Int")
            .Category("Logic").Description("True if A > B (integers).")
            .Input<int>("A", 0).Input<int>("B", 0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") > ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Less Than Int")
            .Category("Logic").Description("True if A < B (integers).")
            .Input<int>("A", 0).Input<int>("B", 0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") < ctx.GetInput<int>("B")))
            .Build();

        // ── String comparison ──

        yield return NodeBuilder.Create("String Equal")
            .Category("Logic").Description("True if two strings are equal (case-sensitive).")
            .Input<string>("A", "").Input<string>("B", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", string.Equals(ctx.GetInput<string>("A"), ctx.GetInput<string>("B"), StringComparison.Ordinal)))
            .Build();

        yield return NodeBuilder.Create("String Equal Ignore Case")
            .Category("Logic").Description("True if two strings are equal (case-insensitive).")
            .Input<string>("A", "").Input<string>("B", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", string.Equals(ctx.GetInput<string>("A"), ctx.GetInput<string>("B"), StringComparison.OrdinalIgnoreCase)))
            .Build();

        // ── Boolean logic ──

        yield return NodeBuilder.Create("And")
            .Category("Logic").Description("Logical AND of two booleans.")
            .Input<bool>("A").Input<bool>("B").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("A") && ctx.GetInput<bool>("B")))
            .Build();

        yield return NodeBuilder.Create("Or")
            .Category("Logic").Description("Logical OR of two booleans.")
            .Input<bool>("A").Input<bool>("B").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("A") || ctx.GetInput<bool>("B")))
            .Build();

        yield return NodeBuilder.Create("Not")
            .Category("Logic").Description("Logical NOT of a boolean.")
            .Input<bool>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", !ctx.GetInput<bool>("Value")))
            .Build();

        yield return NodeBuilder.Create("Xor")
            .Category("Logic").Description("Logical XOR of two booleans.")
            .Input<bool>("A").Input<bool>("B").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("A") ^ ctx.GetInput<bool>("B")))
            .Build();

        // ── Selection ──

        yield return NodeBuilder.Create("Select")
            .Category("Logic").Description("Returns TrueValue if Condition is true, otherwise FalseValue.")
            .Input<bool>("Condition").Input<object>("TrueValue").Input<object>("FalseValue").Output<object>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("Condition") ? ctx.GetInput<object>("TrueValue") : ctx.GetInput<object>("FalseValue")))
            .Build();

        yield return NodeBuilder.Create("Select Number")
            .Category("Logic").Description("Returns TrueValue if Condition is true, otherwise FalseValue (doubles).")
            .Input<bool>("Condition").Input<double>("TrueValue", 0.0).Input<double>("FalseValue", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("Condition") ? ctx.GetInput<double>("TrueValue") : ctx.GetInput<double>("FalseValue")))
            .Build();

        yield return NodeBuilder.Create("Select String")
            .Category("Logic").Description("Returns TrueValue if Condition is true, otherwise FalseValue (strings).")
            .Input<bool>("Condition").Input<string>("TrueValue", "").Input<string>("FalseValue", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<bool>("Condition") ? ctx.GetInput<string>("TrueValue") : ctx.GetInput<string>("FalseValue")))
            .Build();

        // ── Null checks ──

        yield return NodeBuilder.Create("Is Null")
            .Category("Logic").Description("True if the value is null.")
            .Input<object>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<object>("Value") is null))
            .Build();

        yield return NodeBuilder.Create("Is Not Null")
            .Category("Logic").Description("True if the value is not null.")
            .Input<object>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<object>("Value") is not null))
            .Build();

        yield return NodeBuilder.Create("Coalesce")
            .Category("Logic").Description("Returns Value if not null, otherwise Fallback.")
            .Input<object>("Value").Input<object>("Fallback").Output<object>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<object>("Value") ?? ctx.GetInput<object>("Fallback")))
            .Build();

        // ── Boolean constants ──

        yield return NodeBuilder.Create("True")
            .Category("Logic").Description("Constant boolean true.")
            .Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", true))
            .Build();

        yield return NodeBuilder.Create("False")
            .Category("Logic").Description("Constant boolean false.")
            .Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", false))
            .Build();
    }
}
