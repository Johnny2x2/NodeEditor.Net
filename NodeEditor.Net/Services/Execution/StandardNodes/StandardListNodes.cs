using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardListNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("List Create")
            .Category("Lists").Description("Creates an empty list.")
            .Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", new SerializableList()))
            .Build();

        yield return NodeBuilder.Create("List Add")
            .Category("Lists").Description("Adds an item to the end of a list.")
            .Input<SerializableList>("List").Input<object>("Item").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = CloneList(ctx.GetInput<SerializableList>("List"));
                result.Add(ctx.GetInput<object>("Item"));
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Insert")
            .Category("Lists").Description("Inserts an item at the specified index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Input<object>("Item").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = CloneList(ctx.GetInput<SerializableList>("List"));
                result.TryInsert(ctx.GetInput<int>("Index"), ctx.GetInput<object>("Item"));
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Remove At")
            .Category("Lists").Description("Removes the item at the specified index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = CloneList(ctx.GetInput<SerializableList>("List"));
                result.TryRemoveAt(ctx.GetInput<int>("Index"), out _);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Remove Value")
            .Category("Lists").Description("Removes the first occurrence of a value.")
            .Input<SerializableList>("List").Input<object>("Value").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = CloneList(ctx.GetInput<SerializableList>("List"));
                result.TryRemoveValue(ctx.GetInput<object>("Value"));
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Clear")
            .Category("Lists").Description("Returns an empty list.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", new SerializableList()))
            .Build();

        yield return NodeBuilder.Create("List Contains")
            .Category("Lists").Description("Checks if a list contains a value.")
            .Input<SerializableList>("List").Input<object>("Value").Output<bool>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list?.Contains(ctx.GetInput<object>("Value")) ?? false);
            }).Build();

        yield return NodeBuilder.Create("List Index Of")
            .Category("Lists").Description("Returns the index of a value, or -1 if not found.")
            .Input<SerializableList>("List").Input<object>("Value").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list?.IndexOf(ctx.GetInput<object>("Value")) ?? -1);
            }).Build();

        yield return NodeBuilder.Create("List Count")
            .Category("Lists").Description("Returns the number of items in a list.")
            .Input<SerializableList>("List").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                ctx.SetOutput("Result", list?.Count ?? 0);
            }).Build();

        yield return NodeBuilder.Create("List Get")
            .Category("Lists").Description("Gets an item at the specified index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Output<object>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                object value = null!;
                list?.TryGetAt(ctx.GetInput<int>("Index"), out value!);
                ctx.SetOutput("Result", value);
            }).Build();

        yield return NodeBuilder.Create("List Set")
            .Category("Lists").Description("Sets an item at the specified index.")
            .Input<SerializableList>("List").Input<int>("Index", 0).Input<object>("Value").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = CloneList(ctx.GetInput<SerializableList>("List"));
                result.TrySetAt(ctx.GetInput<int>("Index"), ctx.GetInput<object>("Value"));
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("List Slice")
            .Category("Lists").Description("Returns a sub-list from start index with given count.")
            .Input<SerializableList>("List").Input<int>("Start", 0).Input<int>("Count", -1).Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var list = ctx.GetInput<SerializableList>("List");
                var snapshot = list?.Snapshot() ?? Array.Empty<object>();
                var start = Math.Max(0, Math.Min(ctx.GetInput<int>("Start"), snapshot.Length));
                var count = ctx.GetInput<int>("Count");
                var items = count < 0 ? snapshot[start..] : snapshot[start..Math.Min(start + count, snapshot.Length)];
                var result = new SerializableList();
                foreach (var item in items)
                    result.Add(item);
                ctx.SetOutput("Result", result);
            }).Build();
    }

    private static SerializableList CloneList(SerializableList? source)
    {
        var clone = new SerializableList();
        if (source is not null)
        {
            foreach (var item in source.Snapshot())
                clone.Add(item);
        }
        return clone;
    }
}
