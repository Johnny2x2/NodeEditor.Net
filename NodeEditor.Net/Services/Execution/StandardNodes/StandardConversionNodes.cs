using System.Globalization;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardConversionNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        // ── To String ──

        yield return NodeBuilder.Create("To String")
            .Category("Conversion").Description("Converts any value to its string representation.")
            .Input<object>("Value").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<object>("Value")?.ToString() ?? ""))
            .Build();

        yield return NodeBuilder.Create("Number To String")
            .Category("Conversion").Description("Formats a number as a string with optional format specifier.")
            .Input<double>("Value", 0.0).Input<string>("Format", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<double>("Value");
                var format = ctx.GetInput<string>("Format");
                ctx.SetOutput("Result", string.IsNullOrEmpty(format)
                    ? value.ToString(CultureInfo.InvariantCulture)
                    : value.ToString(format, CultureInfo.InvariantCulture));
            }).Build();

        // ── To Int ──

        yield return NodeBuilder.Create("To Int")
            .Category("Conversion").Description("Converts a value to integer. Returns 0 on failure.")
            .Input<object>("Value").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<object>("Value");
                ctx.SetOutput("Result", value switch
                {
                    int i => i,
                    double d => (int)d,
                    float f => (int)f,
                    long l => (int)l,
                    string s when int.TryParse(s, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    bool b => b ? 1 : 0,
                    _ => 0
                });
            }).Build();

        yield return NodeBuilder.Create("Parse Int")
            .Category("Conversion").Description("Parses a string to integer. Returns default on failure.")
            .Input<string>("Value", "0").Input<int>("Default", 0).Output<int>("Result").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                var success = int.TryParse(ctx.GetInput<string>("Value"), CultureInfo.InvariantCulture, out var result);
                ctx.SetOutput("Result", success ? result : ctx.GetInput<int>("Default"));
                ctx.SetOutput("Success", success);
            }).Build();

        // ── To Double ──

        yield return NodeBuilder.Create("To Double")
            .Category("Conversion").Description("Converts a value to double. Returns 0 on failure.")
            .Input<object>("Value").Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<object>("Value");
                ctx.SetOutput("Result", value switch
                {
                    double d => d,
                    int i => (double)i,
                    float f => (double)f,
                    long l => (double)l,
                    string s when double.TryParse(s, CultureInfo.InvariantCulture, out var parsed) => parsed,
                    bool b => b ? 1.0 : 0.0,
                    _ => 0.0
                });
            }).Build();

        yield return NodeBuilder.Create("Parse Double")
            .Category("Conversion").Description("Parses a string to double. Returns default on failure.")
            .Input<string>("Value", "0").Input<double>("Default", 0.0).Output<double>("Result").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                var success = double.TryParse(ctx.GetInput<string>("Value"), CultureInfo.InvariantCulture, out var result);
                ctx.SetOutput("Result", success ? result : ctx.GetInput<double>("Default"));
                ctx.SetOutput("Success", success);
            }).Build();

        // ── To Bool ──

        yield return NodeBuilder.Create("To Bool")
            .Category("Conversion").Description("Converts a value to boolean. 0, null, empty string, and \"false\" are false; everything else is true.")
            .Input<object>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<object>("Value");
                ctx.SetOutput("Result", value switch
                {
                    null => false,
                    bool b => b,
                    int i => i != 0,
                    double d => d != 0.0,
                    string s => !string.IsNullOrEmpty(s) && !s.Equals("false", StringComparison.OrdinalIgnoreCase) && s != "0",
                    _ => true
                });
            }).Build();

        yield return NodeBuilder.Create("Parse Bool")
            .Category("Conversion").Description("Parses a string to boolean.")
            .Input<string>("Value", "false").Output<bool>("Result").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                var success = bool.TryParse(ctx.GetInput<string>("Value"), out var result);
                ctx.SetOutput("Result", result);
                ctx.SetOutput("Success", success);
            }).Build();

        // ── Int ↔ Double ──

        yield return NodeBuilder.Create("Int To Double")
            .Category("Conversion").Description("Converts an integer to a double.")
            .Input<int>("Value", 0).Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (double)ctx.GetInput<int>("Value")))
            .Build();

        yield return NodeBuilder.Create("Double To Int")
            .Category("Conversion").Description("Truncates a double to an integer.")
            .Input<double>("Value", 0.0).Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (int)ctx.GetInput<double>("Value")))
            .Build();

        // ── Char operations ──

        yield return NodeBuilder.Create("Char At")
            .Category("Conversion").Description("Gets the character at the specified index.")
            .Input<string>("Value", "").Input<int>("Index", 0).Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<string>("Value") ?? "";
                var index = ctx.GetInput<int>("Index");
                ctx.SetOutput("Result", index >= 0 && index < value.Length ? value[index].ToString() : "");
            }).Build();

        yield return NodeBuilder.Create("Char Code")
            .Category("Conversion").Description("Gets the integer char code of the first character.")
            .Input<string>("Value", "").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<string>("Value") ?? "";
                ctx.SetOutput("Result", value.Length > 0 ? (int)value[0] : 0);
            }).Build();

        yield return NodeBuilder.Create("From Char Code")
            .Category("Conversion").Description("Converts an integer char code to a string.")
            .Input<int>("Code", 65).Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ((char)ctx.GetInput<int>("Code")).ToString()))
            .Build();

        // ── Type info ──

        yield return NodeBuilder.Create("Type Name")
            .Category("Conversion").Description("Gets the type name of a value.")
            .Input<object>("Value").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<object>("Value");
                ctx.SetOutput("Result", value?.GetType().Name ?? "null");
            }).Build();
    }
}
