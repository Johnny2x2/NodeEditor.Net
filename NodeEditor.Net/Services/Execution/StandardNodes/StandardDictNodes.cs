using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardDictNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("Dict Create")
            .Category("Dictionaries").Description("Creates an empty dictionary.")
            .Output<SerializableDict>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", new SerializableDict()))
            .Build();

        yield return NodeBuilder.Create("Dict Set")
            .Category("Dictionaries").Description("Sets a key-value pair in a dictionary.")
            .Input<SerializableDict>("Dict").Input<string>("Key", "").Input<object>("Value").Output<SerializableDict>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = ctx.GetInput<SerializableDict>("Dict")?.Clone() ?? new SerializableDict();
                result.Set(ctx.GetInput<string>("Key") ?? "", ctx.GetInput<object>("Value"));
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("Dict Get")
            .Category("Dictionaries").Description("Gets a value by key. Outputs null if not found.")
            .Input<SerializableDict>("Dict").Input<string>("Key", "").Output<object>("Value").Output<bool>("Found")
            .OnExecute(async (ctx, ct) =>
            {
                var dict = ctx.GetInput<SerializableDict>("Dict");
                object value = null!;
                var found = dict?.TryGet(ctx.GetInput<string>("Key") ?? "", out value!) ?? false;
                ctx.SetOutput("Value", found ? value : null!);
                ctx.SetOutput("Found", found);
            }).Build();

        yield return NodeBuilder.Create("Dict Remove")
            .Category("Dictionaries").Description("Removes a key from a dictionary.")
            .Input<SerializableDict>("Dict").Input<string>("Key", "").Output<SerializableDict>("Result").Output<bool>("Removed")
            .OnExecute(async (ctx, ct) =>
            {
                var result = ctx.GetInput<SerializableDict>("Dict")?.Clone() ?? new SerializableDict();
                var removed = result.Remove(ctx.GetInput<string>("Key") ?? "");
                ctx.SetOutput("Result", result);
                ctx.SetOutput("Removed", removed);
            }).Build();

        yield return NodeBuilder.Create("Dict Contains Key")
            .Category("Dictionaries").Description("Checks if a dictionary contains a key.")
            .Input<SerializableDict>("Dict").Input<string>("Key", "").Output<bool>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var dict = ctx.GetInput<SerializableDict>("Dict");
                ctx.SetOutput("Result", dict?.ContainsKey(ctx.GetInput<string>("Key") ?? "") ?? false);
            }).Build();

        yield return NodeBuilder.Create("Dict Keys")
            .Category("Dictionaries").Description("Returns all keys as a list.")
            .Input<SerializableDict>("Dict").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var dict = ctx.GetInput<SerializableDict>("Dict");
                var list = new SerializableList();
                if (dict is not null)
                    foreach (var key in dict.Keys())
                        list.Add(key);
                ctx.SetOutput("Result", list);
            }).Build();

        yield return NodeBuilder.Create("Dict Values")
            .Category("Dictionaries").Description("Returns all values as a list.")
            .Input<SerializableDict>("Dict").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var dict = ctx.GetInput<SerializableDict>("Dict");
                var list = new SerializableList();
                if (dict is not null)
                    foreach (var value in dict.Values())
                        list.Add(value);
                ctx.SetOutput("Result", list);
            }).Build();

        yield return NodeBuilder.Create("Dict Count")
            .Category("Dictionaries").Description("Returns the number of entries in a dictionary.")
            .Input<SerializableDict>("Dict").Output<int>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", ctx.GetInput<SerializableDict>("Dict")?.Count ?? 0))
            .Build();

        yield return NodeBuilder.Create("Dict Clear")
            .Category("Dictionaries").Description("Returns an empty dictionary.")
            .Input<SerializableDict>("Dict").Output<SerializableDict>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", new SerializableDict()))
            .Build();

        yield return NodeBuilder.Create("Dict Merge")
            .Category("Dictionaries").Description("Merges two dictionaries. B's keys overwrite A's on collision.")
            .Input<SerializableDict>("A").Input<SerializableDict>("B").Output<SerializableDict>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var result = ctx.GetInput<SerializableDict>("A")?.Clone() ?? new SerializableDict();
                var b = ctx.GetInput<SerializableDict>("B");
                if (b is not null)
                    foreach (var kvp in b.Snapshot())
                        result.Set(kvp.Key, kvp.Value);
                ctx.SetOutput("Result", result);
            }).Build();
    }
}
