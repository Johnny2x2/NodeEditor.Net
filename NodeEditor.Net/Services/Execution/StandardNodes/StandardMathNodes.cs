using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardMathNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        // ── Basic arithmetic ──

        yield return NodeBuilder.Create("Add")
            .Category("Math").Description("Adds two numbers.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") + ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Subtract")
            .Category("Math").Description("Subtracts B from A.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") - ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Multiply")
            .Category("Math").Description("Multiplies two numbers.")
            .Input<double>("A", 0.0).Input<double>("B", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("A") * ctx.GetInput<double>("B")))
            .Build();

        yield return NodeBuilder.Create("Divide")
            .Category("Math").Description("Divides A by B. Returns 0 if B is 0.")
            .Input<double>("A", 0.0).Input<double>("B", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var b = ctx.GetInput<double>("B");
                ctx.SetOutput("Result", b == 0 ? 0.0 : ctx.GetInput<double>("A") / b);
            }).Build();

        yield return NodeBuilder.Create("Negate")
            .Category("Math").Description("Negates a number.")
            .Input<double>("Value", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", -ctx.GetInput<double>("Value")))
            .Build();

        yield return NodeBuilder.Create("Power")
            .Category("Math").Description("Raises Base to the Exponent power.")
            .Input<double>("Base", 0.0).Input<double>("Exponent", 2.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Pow(ctx.GetInput<double>("Base"), ctx.GetInput<double>("Exponent"))))
            .Build();

        yield return NodeBuilder.Create("Sqrt")
            .Category("Math").Description("Square root of a number.")
            .Input<double>("Value", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Sqrt(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Log")
            .Category("Math").Description("Natural logarithm (ln) of a number.")
            .Input<double>("Value", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Log(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Log10")
            .Category("Math").Description("Base-10 logarithm of a number.")
            .Input<double>("Value", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Log10(ctx.GetInput<double>("Value"))))
            .Build();

        // ── Trigonometry ──

        yield return NodeBuilder.Create("Sin")
            .Category("Math").Description("Sine of an angle in radians.")
            .Input<double>("Radians", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Sin(ctx.GetInput<double>("Radians"))))
            .Build();

        yield return NodeBuilder.Create("Cos")
            .Category("Math").Description("Cosine of an angle in radians.")
            .Input<double>("Radians", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Cos(ctx.GetInput<double>("Radians"))))
            .Build();

        yield return NodeBuilder.Create("Tan")
            .Category("Math").Description("Tangent of an angle in radians.")
            .Input<double>("Radians", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Tan(ctx.GetInput<double>("Radians"))))
            .Build();

        yield return NodeBuilder.Create("Atan2")
            .Category("Math").Description("Angle in radians from Y and X coordinates.")
            .Input<double>("Y", 0.0).Input<double>("X", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Atan2(ctx.GetInput<double>("Y"), ctx.GetInput<double>("X"))))
            .Build();

        // ── Constants ──

        yield return NodeBuilder.Create("PI")
            .Category("Math").Description("The constant π (3.14159…).")
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.PI))
            .Build();

        yield return NodeBuilder.Create("E")
            .Category("Math").Description("Euler's number e (2.71828…).")
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.E))
            .Build();

        // ── Integer arithmetic ──

        yield return NodeBuilder.Create("Add Int")
            .Category("Math").Description("Adds two integers.")
            .Input<int>("A", 0).Input<int>("B", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") + ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Subtract Int")
            .Category("Math").Description("Subtracts B from A (integers).")
            .Input<int>("A", 0).Input<int>("B", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") - ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Multiply Int")
            .Category("Math").Description("Multiplies two integers.")
            .Input<int>("A", 0).Input<int>("B", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") * ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Divide Int")
            .Category("Math").Description("Integer division of A by B. Returns 0 if B is 0.")
            .Input<int>("A", 0).Input<int>("B", 1).Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var b = ctx.GetInput<int>("B");
                ctx.SetOutput("Result", b == 0 ? 0 : ctx.GetInput<int>("A") / b);
            }).Build();

        yield return NodeBuilder.Create("Increment")
            .Category("Math").Description("Adds 1 to the value.")
            .Input<int>("Value", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value") + 1))
            .Build();

        yield return NodeBuilder.Create("Decrement")
            .Category("Math").Description("Subtracts 1 from the value.")
            .Input<int>("Value", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value") - 1))
            .Build();

        // ── Bitwise ──

        yield return NodeBuilder.Create("Bitwise And")
            .Category("Math").Description("Bitwise AND of two integers.")
            .Input<int>("A", 0).Input<int>("B", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") & ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Bitwise Or")
            .Category("Math").Description("Bitwise OR of two integers.")
            .Input<int>("A", 0).Input<int>("B", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") | ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Bitwise Xor")
            .Category("Math").Description("Bitwise XOR of two integers.")
            .Input<int>("A", 0).Input<int>("B", 0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("A") ^ ctx.GetInput<int>("B")))
            .Build();

        yield return NodeBuilder.Create("Shift Left")
            .Category("Math").Description("Left bit shift.")
            .Input<int>("Value", 0).Input<int>("Shift", 1).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value") << ctx.GetInput<int>("Shift")))
            .Build();

        yield return NodeBuilder.Create("Shift Right")
            .Category("Math").Description("Right bit shift.")
            .Input<int>("Value", 0).Input<int>("Shift", 1).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value") >> ctx.GetInput<int>("Shift")))
            .Build();
    }
}
