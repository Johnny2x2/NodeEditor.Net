using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardStringNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("String Concat")
            .Category("Strings").Description("Concatenates two strings.")
            .Input<string>("A", "").Input<string>("B", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<string>("A") + ctx.GetInput<string>("B")))
            .Build();

        yield return NodeBuilder.Create("String Length")
            .Category("Strings").Description("Returns the length of a string.")
            .Input<string>("Value", "").Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Length))
            .Build();

        yield return NodeBuilder.Create("Substring")
            .Category("Strings").Description("Extracts a substring.")
            .Input<string>("Value", "").Input<int>("Start", 0).Input<int>("Length", -1).Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<string>("Value") ?? "";
                var start = Math.Max(0, Math.Min(ctx.GetInput<int>("Start"), value.Length));
                var length = ctx.GetInput<int>("Length");
                ctx.SetOutput("Result", length < 0 ? value[start..] : value.Substring(start, Math.Min(length, value.Length - start)));
            }).Build();

        yield return NodeBuilder.Create("Replace")
            .Category("Strings").Description("Replaces occurrences of a substring.")
            .Input<string>("Value", "").Input<string>("Old", "").Input<string>("New", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Replace(ctx.GetInput<string>("Old") ?? "", ctx.GetInput<string>("New") ?? "")))
            .Build();

        yield return NodeBuilder.Create("To Upper")
            .Category("Strings").Description("Converts to uppercase.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").ToUpperInvariant()))
            .Build();

        yield return NodeBuilder.Create("To Lower")
            .Category("Strings").Description("Converts to lowercase.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").ToLowerInvariant()))
            .Build();

        yield return NodeBuilder.Create("Trim")
            .Category("Strings").Description("Trims whitespace.")
            .Input<string>("Value", "").Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Trim()))
            .Build();

        yield return NodeBuilder.Create("Contains")
            .Category("Strings").Description("Checks if string contains a substring.")
            .Input<string>("Value", "").Input<string>("Search", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").Contains(ctx.GetInput<string>("Search") ?? "")))
            .Build();

        yield return NodeBuilder.Create("Starts With")
            .Category("Strings").Description("Checks if string starts with a prefix.")
            .Input<string>("Value", "").Input<string>("Prefix", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").StartsWith(ctx.GetInput<string>("Prefix") ?? "")))
            .Build();

        yield return NodeBuilder.Create("Ends With")
            .Category("Strings").Description("Checks if string ends with a suffix.")
            .Input<string>("Value", "").Input<string>("Suffix", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (ctx.GetInput<string>("Value") ?? "").EndsWith(ctx.GetInput<string>("Suffix") ?? "")))
            .Build();

        yield return NodeBuilder.Create("Split")
            .Category("Strings").Description("Splits a string by a delimiter into a list.")
            .Input<string>("Value", "").Input<string>("Delimiter", ",").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var parts = (ctx.GetInput<string>("Value") ?? "").Split(ctx.GetInput<string>("Delimiter") ?? ",");
                var list = new SerializableList();
                foreach (var part in parts)
                    list.Add(part);
                ctx.SetOutput("Result", list);
            }).Build();

        yield return NodeBuilder.Create("Join")
            .Category("Strings").Description("Joins a list into a string with a separator.")
            .Input<SerializableList>("List").Input<string>("Separator", ", ").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var items = list?.Snapshot().Select(i => i?.ToString() ?? "") ?? Enumerable.Empty<string>();
                ctx.SetOutput("Result", string.Join(ctx.GetInput<string>("Separator") ?? ", ", items));
            }).Build();
    }
}
