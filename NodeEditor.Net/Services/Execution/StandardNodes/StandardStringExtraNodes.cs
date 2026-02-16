using System.Globalization;
using System.Text.RegularExpressions;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardStringExtraNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        // ── Formatting ──

        yield return NodeBuilder.Create("String Format")
            .Category("Strings").Description("Formats a string using {0}, {1}, {2}, {3} placeholders.")
            .Input<string>("Format", "").Input<object>("Arg0").Input<object>("Arg1").Input<object>("Arg2").Input<object>("Arg3")
            .Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var fmt = ctx.GetInput<string>("Format") ?? "";
                var a0 = ctx.GetInput<object>("Arg0");
                var a1 = ctx.GetInput<object>("Arg1");
                var a2 = ctx.GetInput<object>("Arg2");
                var a3 = ctx.GetInput<object>("Arg3");
                try { ctx.SetOutput("Result", string.Format(CultureInfo.InvariantCulture, fmt, a0, a1, a2, a3)); }
                catch { ctx.SetOutput("Result", fmt); }
            }).Build();

        yield return NodeBuilder.Create("String Is Empty")
            .Category("Strings").Description("True if the string is null or empty.")
            .Input<string>("Value", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", string.IsNullOrEmpty(ctx.GetInput<string>("Value"))))
            .Build();

        yield return NodeBuilder.Create("String Is Whitespace")
            .Category("Strings").Description("True if the string is null, empty, or only whitespace.")
            .Input<string>("Value", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", string.IsNullOrWhiteSpace(ctx.GetInput<string>("Value"))))
            .Build();

        // ── Search and extraction ──

        yield return NodeBuilder.Create("Index Of")
            .Category("Strings").Description("Finds the first index of a substring, or -1 if not found.")
            .Input<string>("Value", "").Input<string>("Search", "").Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").IndexOf(ctx.GetInput<string>("Search") ?? "", StringComparison.Ordinal)))
            .Build();

        yield return NodeBuilder.Create("Last Index Of")
            .Category("Strings").Description("Finds the last index of a substring, or -1 if not found.")
            .Input<string>("Value", "").Input<string>("Search", "").Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").LastIndexOf(ctx.GetInput<string>("Search") ?? "", StringComparison.Ordinal)))
            .Build();

        yield return NodeBuilder.Create("Regex Match")
            .Category("Strings").Description("Returns the first regex match, or empty string if no match.")
            .Input<string>("Value", "").Input<string>("Pattern", "").Output<string>("Match").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                try
                {
                    var m = Regex.Match(ctx.GetInput<string>("Value") ?? "", ctx.GetInput<string>("Pattern") ?? "");
                    ctx.SetOutput("Match", m.Success ? m.Value : "");
                    ctx.SetOutput("Success", m.Success);
                }
                catch
                {
                    ctx.SetOutput("Match", "");
                    ctx.SetOutput("Success", false);
                }
            }).Build();

        yield return NodeBuilder.Create("Regex Replace")
            .Category("Strings").Description("Replaces regex matches in a string.")
            .Input<string>("Value", "").Input<string>("Pattern", "").Input<string>("Replacement", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                try
                {
                    ctx.SetOutput("Result", Regex.Replace(ctx.GetInput<string>("Value") ?? "", ctx.GetInput<string>("Pattern") ?? "", ctx.GetInput<string>("Replacement") ?? ""));
                }
                catch
                {
                    ctx.SetOutput("Result", ctx.GetInput<string>("Value") ?? "");
                }
            }).Build();

        // ── Padding and repeat ──

        yield return NodeBuilder.Create("Pad Left")
            .Category("Strings").Description("Pads a string on the left to reach a desired length.")
            .Input<string>("Value", "").Input<int>("TotalWidth", 10).Input<string>("PadChar", " ").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var padChar = ctx.GetInput<string>("PadChar");
                var ch = string.IsNullOrEmpty(padChar) ? ' ' : padChar[0];
                ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").PadLeft(ctx.GetInput<int>("TotalWidth"), ch));
            }).Build();

        yield return NodeBuilder.Create("Pad Right")
            .Category("Strings").Description("Pads a string on the right to reach a desired length.")
            .Input<string>("Value", "").Input<int>("TotalWidth", 10).Input<string>("PadChar", " ").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var padChar = ctx.GetInput<string>("PadChar");
                var ch = string.IsNullOrEmpty(padChar) ? ' ' : padChar[0];
                ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").PadRight(ctx.GetInput<int>("TotalWidth"), ch));
            }).Build();

        yield return NodeBuilder.Create("Repeat String")
            .Category("Strings").Description("Repeats a string N times.")
            .Input<string>("Value", "").Input<int>("Count", 1).Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<string>("Value") ?? "";
                var count = Math.Max(0, Math.Min(ctx.GetInput<int>("Count"), 10_000));
                ctx.SetOutput("Result", string.Concat(Enumerable.Repeat(value, count)));
            }).Build();

        yield return NodeBuilder.Create("Reverse String")
            .Category("Strings").Description("Reverses a string.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var chars = (ctx.GetInput<string>("Value") ?? "").ToCharArray();
                Array.Reverse(chars);
                ctx.SetOutput("Result", new string(chars));
            }).Build();

        // ── New line / concatenation helpers ──

        yield return NodeBuilder.Create("New Line")
            .Category("Strings").Description("Outputs the newline character sequence.")
            .Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Environment.NewLine))
            .Build();

        yield return NodeBuilder.Create("String Concat 3")
            .Category("Strings").Description("Concatenates three strings.")
            .Input<string>("A", "").Input<string>("B", "").Input<string>("C", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result",
                (ctx.GetInput<string>("A") ?? "") + (ctx.GetInput<string>("B") ?? "") + (ctx.GetInput<string>("C") ?? "")))
            .Build();

        yield return NodeBuilder.Create("String Concat 4")
            .Category("Strings").Description("Concatenates four strings.")
            .Input<string>("A", "").Input<string>("B", "").Input<string>("C", "").Input<string>("D", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result",
                (ctx.GetInput<string>("A") ?? "") + (ctx.GetInput<string>("B") ?? "") +
                (ctx.GetInput<string>("C") ?? "") + (ctx.GetInput<string>("D") ?? "")))
            .Build();
    }
}
