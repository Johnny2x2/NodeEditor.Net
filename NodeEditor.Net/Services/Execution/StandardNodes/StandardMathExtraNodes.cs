using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardMathExtraNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        // ── Interpolation ──

        yield return NodeBuilder.Create("Lerp")
            .Category("Math").Description("Linearly interpolates between A and B by T (0–1).")
            .Input<double>("A", 0.0).Input<double>("B", 1.0).Input<double>("T", 0.5).Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var a = ctx.GetInput<double>("A");
                var b = ctx.GetInput<double>("B");
                var t = ctx.GetInput<double>("T");
                ctx.SetOutput("Result", a + (b - a) * t);
            }).Build();

        yield return NodeBuilder.Create("Inverse Lerp")
            .Category("Math").Description("Returns where Value falls between A and B (0–1).")
            .Input<double>("A", 0.0).Input<double>("B", 1.0).Input<double>("Value", 0.5).Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var a = ctx.GetInput<double>("A");
                var b = ctx.GetInput<double>("B");
                var v = ctx.GetInput<double>("Value");
                ctx.SetOutput("Result", Math.Abs(b - a) < double.Epsilon ? 0.0 : (v - a) / (b - a));
            }).Build();

        yield return NodeBuilder.Create("Map Range")
            .Category("Math").Description("Maps a value from one range to another.")
            .Input<double>("Value", 0.5)
            .Input<double>("InMin", 0.0).Input<double>("InMax", 1.0)
            .Input<double>("OutMin", 0.0).Input<double>("OutMax", 100.0)
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var v = ctx.GetInput<double>("Value");
                var inMin = ctx.GetInput<double>("InMin");
                var inMax = ctx.GetInput<double>("InMax");
                var outMin = ctx.GetInput<double>("OutMin");
                var outMax = ctx.GetInput<double>("OutMax");
                var range = inMax - inMin;
                var t = Math.Abs(range) < double.Epsilon ? 0.0 : (v - inMin) / range;
                ctx.SetOutput("Result", outMin + (outMax - outMin) * t);
            }).Build();

        yield return NodeBuilder.Create("Smoothstep")
            .Category("Math").Description("Hermite interpolation between 0 and 1.")
            .Input<double>("Edge0", 0.0).Input<double>("Edge1", 1.0).Input<double>("Value", 0.5).Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var edge0 = ctx.GetInput<double>("Edge0");
                var edge1 = ctx.GetInput<double>("Edge1");
                var x = ctx.GetInput<double>("Value");
                var t = Math.Clamp((x - edge0) / (edge1 - edge0 + double.Epsilon), 0.0, 1.0);
                ctx.SetOutput("Result", t * t * (3.0 - 2.0 * t));
            }).Build();

        // ── Distance ──

        yield return NodeBuilder.Create("Distance 2D")
            .Category("Math").Description("Euclidean distance between two 2D points.")
            .Input<double>("X1", 0.0).Input<double>("Y1", 0.0)
            .Input<double>("X2", 1.0).Input<double>("Y2", 1.0)
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var dx = ctx.GetInput<double>("X2") - ctx.GetInput<double>("X1");
                var dy = ctx.GetInput<double>("Y2") - ctx.GetInput<double>("Y1");
                ctx.SetOutput("Result", Math.Sqrt(dx * dx + dy * dy));
            }).Build();

        yield return NodeBuilder.Create("Dot Product 2D")
            .Category("Math").Description("Dot product of two 2D vectors.")
            .Input<double>("X1", 0.0).Input<double>("Y1", 0.0)
            .Input<double>("X2", 0.0).Input<double>("Y2", 0.0)
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                ctx.SetOutput("Result",
                    ctx.GetInput<double>("X1") * ctx.GetInput<double>("X2") +
                    ctx.GetInput<double>("Y1") * ctx.GetInput<double>("Y2"));
            }).Build();

        // ── Conversions / utility ──

        yield return NodeBuilder.Create("Deg To Rad")
            .Category("Math").Description("Converts degrees to radians.")
            .Input<double>("Degrees", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("Degrees") * Math.PI / 180.0))
            .Build();

        yield return NodeBuilder.Create("Rad To Deg")
            .Category("Math").Description("Converts radians to degrees.")
            .Input<double>("Radians", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<double>("Radians") * 180.0 / Math.PI))
            .Build();

        yield return NodeBuilder.Create("Exp")
            .Category("Math").Description("Returns e raised to the specified power.")
            .Input<double>("Value", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Exp(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Truncate")
            .Category("Math").Description("Truncates the decimal part of a number.")
            .Input<double>("Value", 0.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Math.Truncate(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Is Even")
            .Category("Math").Description("True if the integer is even.")
            .Input<int>("Value", 0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value") % 2 == 0))
            .Build();

        yield return NodeBuilder.Create("Is Odd")
            .Category("Math").Description("True if the integer is odd.")
            .Input<int>("Value", 0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<int>("Value") % 2 != 0))
            .Build();

        yield return NodeBuilder.Create("Is NaN")
            .Category("Math").Description("True if the value is NaN.")
            .Input<double>("Value", 0.0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", double.IsNaN(ctx.GetInput<double>("Value"))))
            .Build();

        yield return NodeBuilder.Create("Is Infinity")
            .Category("Math").Description("True if the value is positive or negative infinity.")
            .Input<double>("Value", 0.0).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", double.IsInfinity(ctx.GetInput<double>("Value"))))
            .Build();

        // ── Accumulate (sum across multiple calls) ──

        yield return NodeBuilder.Create("Running Sum")
            .Category("Math").Description("Adds each input to a running total. Connect Reset to clear.")
            .Input<double>("Value", 0.0).Input<bool>("Reset").Output<double>("Total")
            .OnExecute(async (ctx, ct) =>
            {
                var key = $"__running_sum_{ctx.Node.Id}";
                if (ctx.GetInput<bool>("Reset"))
                    ctx.SetVariable(key, 0.0);
                var current = ctx.GetVariable(key) switch { double d => d, int i => (double)i, _ => 0.0 };
                current += ctx.GetInput<double>("Value");
                ctx.SetVariable(key, current);
                ctx.SetOutput("Total", current);
            }).Build();
    }
}
