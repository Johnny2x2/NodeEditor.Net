using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardListExtraNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("List Reverse")
            .Category("Lists").Description("Reverses a list.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                Array.Reverse(snapshot);
                var result = new SerializableList();
                foreach (var item in snapshot) result.Add(item);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Sort")
            .Category("Lists").Description("Sorts a list of comparable items in ascending order.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                Array.Sort(snapshot, (a, b) =>
                {
                    if (a is IComparable ca) return ca.CompareTo(b);
                    return string.Compare(a?.ToString(), b?.ToString(), StringComparison.Ordinal);
                });
                var result = new SerializableList();
                foreach (var item in snapshot) result.Add(item);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Sort Descending")
            .Category("Lists").Description("Sorts a list in descending order.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                Array.Sort(snapshot, (a, b) =>
                {
                    if (b is IComparable cb) return cb.CompareTo(a);
                    return string.Compare(b?.ToString(), a?.ToString(), StringComparison.Ordinal);
                });
                var result = new SerializableList();
                foreach (var item in snapshot) result.Add(item);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List First")
            .Category("Lists").Description("Returns the first item in a list, or null if empty.")
            .Input<SerializableList>("List").Output<object>("Result").Output<bool>("HasValue")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var snapshot = list?.Snapshot() ?? Array.Empty<object>();
                ctx.SetOutput("Result", snapshot.Length > 0 ? snapshot[0] : null!);
                ctx.SetOutput("HasValue", snapshot.Length > 0);
            }).Build();

        yield return NodeBuilder.Create("List Last")
            .Category("Lists").Description("Returns the last item in a list, or null if empty.")
            .Input<SerializableList>("List").Output<object>("Result").Output<bool>("HasValue")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var snapshot = list?.Snapshot() ?? Array.Empty<object>();
                ctx.SetOutput("Result", snapshot.Length > 0 ? snapshot[^1] : null!);
                ctx.SetOutput("HasValue", snapshot.Length > 0);
            }).Build();

        yield return NodeBuilder.Create("List Distinct")
            .Category("Lists").Description("Removes duplicate items from a list.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                var seen = new HashSet<object>();
                var result = new SerializableList();
                foreach (var item in snapshot)
                {
                    if (seen.Add(item))
                        result.Add(item);
                }
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Flatten")
            .Category("Lists").Description("Flattens nested lists into a single list.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = new SerializableList();
                void Flatten(SerializableList? list)
                {
                    if (list is null) return;
                    foreach (var item in list.Snapshot())
                    {
                        if (item is SerializableList inner)
                            Flatten(inner);
                        else
                            result.Add(item);
                    }
                }
                Flatten(ctx.GetInput<SerializableList>("List"));
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Concat")
            .Category("Lists").Description("Concatenates two lists.")
            .Input<SerializableList>("A").Input<SerializableList>("B").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = new SerializableList();
                var a = ctx.GetInput<SerializableList>("A")?.Snapshot() ?? Array.Empty<object>();
                var b = ctx.GetInput<SerializableList>("B")?.Snapshot() ?? Array.Empty<object>();
                foreach (var item in a) result.Add(item);
                foreach (var item in b) result.Add(item);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Range")
            .Category("Lists").Description("Creates a list of integers from Start to End (exclusive).")
            .Input<int>("Start", 0).Input<int>("End", 10).Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var start = ctx.GetInput<int>("Start");
                var end = ctx.GetInput<int>("End");
                var result = new SerializableList();
                var count = 0;
                if (start <= end)
                {
                    for (int i = start; i < end && count < 100_000; i++, count++)
                        result.Add(i);
                }
                else
                {
                    for (int i = start; i > end && count < 100_000; i--, count++)
                        result.Add(i);
                }
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Repeat")
            .Category("Lists").Description("Creates a list containing the same value repeated N times.")
            .Input<object>("Value").Input<int>("Count", 5).Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<object>("Value");
                var count = Math.Max(0, Math.Min(ctx.GetInput<int>("Count"), 100_000));
                var result = new SerializableList();
                for (int i = 0; i < count; i++)
                    result.Add(value);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Is Empty")
            .Category("Lists").Description("True if the list is null or has zero items.")
            .Input<SerializableList>("List").Output<bool>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list is null || list.Count == 0);
            }).Build();

        yield return NodeBuilder.Create("List Any")
            .Category("Lists").Description("True if the list has at least one item.")
            .Input<SerializableList>("List").Output<bool>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list is not null && list.Count > 0);
            }).Build();

        yield return NodeBuilder.Create("List Sum")
            .Category("Lists").Description("Sums all numeric items in a list.")
            .Input<SerializableList>("List").Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                double sum = 0;
                foreach (var item in snapshot)
                {
                    sum += item switch
                    {
                        double d => d,
                        int i => i,
                        float f => f,
                        long l => l,
                        string s when double.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out var p) => p,
                        _ => 0
                    };
                }
                ctx.SetOutput("Result", sum);
            }).Build();

        yield return NodeBuilder.Create("List Average")
            .Category("Lists").Description("Averages all numeric items in a list.")
            .Input<SerializableList>("List").Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                if (snapshot.Length == 0) { ctx.SetOutput("Result", 0.0); return; }
                double sum = 0;
                foreach (var item in snapshot)
                {
                    sum += item switch
                    {
                        double d => d,
                        int i => i,
                        float f => f,
                        long l => l,
                        _ => 0
                    };
                }
                ctx.SetOutput("Result", sum / snapshot.Length);
            }).Build();

        yield return NodeBuilder.Create("List Min")
            .Category("Lists").Description("Returns the minimum numeric value from a list.")
            .Input<SerializableList>("List").Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                double min = double.MaxValue;
                bool found = false;
                foreach (var item in snapshot)
                {
                    double v = item switch
                    {
                        double d => d,
                        int i => i,
                        float f => f,
                        long l => l,
                        _ => double.NaN
                    };
                    if (!double.IsNaN(v)) { min = Math.Min(min, v); found = true; }
                }
                ctx.SetOutput("Result", found ? min : 0.0);
            }).Build();

        yield return NodeBuilder.Create("List Max")
            .Category("Lists").Description("Returns the maximum numeric value from a list.")
            .Input<SerializableList>("List").Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                double max = double.MinValue;
                bool found = false;
                foreach (var item in snapshot)
                {
                    double v = item switch
                    {
                        double d => d,
                        int i => i,
                        float f => f,
                        long l => l,
                        _ => double.NaN
                    };
                    if (!double.IsNaN(v)) { max = Math.Max(max, v); found = true; }
                }
                ctx.SetOutput("Result", found ? max : 0.0);
            }).Build();

        yield return NodeBuilder.Create("List To String")
            .Category("Lists").Description("Converts each item in a list to string.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                var result = new SerializableList();
                foreach (var item in snapshot)
                    result.Add(item?.ToString() ?? "");
                ctx.SetOutput("Result", result);
            }).Build();
    }
}
