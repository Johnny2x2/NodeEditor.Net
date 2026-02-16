using System.Text.Json;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardJsonNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("JSON Parse")
            .Category("JSON").Description("Parses a JSON string into an object (JsonElement).")
            .Input<string>("Json", "{}").Output<object>("Result").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                try
                {
                    var json = ctx.GetInput<string>("Json") ?? "{}";
                    var element = JsonDocument.Parse(json).RootElement.Clone();
                    ctx.SetOutput("Result", (object)element);
                    ctx.SetOutput("Success", true);
                }
                catch
                {
                    ctx.SetOutput("Result", null!);
                    ctx.SetOutput("Success", false);
                }
            }).Build();

        yield return NodeBuilder.Create("JSON Stringify")
            .Category("JSON").Description("Serializes a value to a JSON string.")
            .Input<object>("Value").Input<bool>("Indented").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var value = ctx.GetInput<object>("Value");
                var indented = ctx.GetInput<bool>("Indented");
                var options = new JsonSerializerOptions { WriteIndented = indented };
                try { ctx.SetOutput("Result", JsonSerializer.Serialize(value, value?.GetType() ?? typeof(object), options)); }
                catch { ctx.SetOutput("Result", "null"); }
            }).Build();

        yield return NodeBuilder.Create("JSON Get Property")
            .Category("JSON").Description("Gets a property from a JSON object by name.")
            .Input<object>("Json").Input<string>("Property", "").Output<object>("Value").Output<bool>("Found")
            .OnExecute(async (ctx, ct) =>
            {
                var jsonObj = ctx.GetInput<object>("Json");
                var prop = ctx.GetInput<string>("Property") ?? "";
                if (jsonObj is JsonElement el && el.ValueKind == JsonValueKind.Object && el.TryGetProperty(prop, out var value))
                {
                    ctx.SetOutput("Value", (object)UnwrapJsonElement(value));
                    ctx.SetOutput("Found", true);
                }
                else
                {
                    ctx.SetOutput("Value", null!);
                    ctx.SetOutput("Found", false);
                }
            }).Build();

        yield return NodeBuilder.Create("JSON Get Index")
            .Category("JSON").Description("Gets an element from a JSON array by index.")
            .Input<object>("Json").Input<int>("Index", 0).Output<object>("Value").Output<bool>("Found")
            .OnExecute(async (ctx, ct) =>
            {
                var jsonObj = ctx.GetInput<object>("Json");
                var index = ctx.GetInput<int>("Index");
                if (jsonObj is JsonElement el && el.ValueKind == JsonValueKind.Array)
                {
                    var length = el.GetArrayLength();
                    if (index >= 0 && index < length)
                    {
                        ctx.SetOutput("Value", (object)UnwrapJsonElement(el[index]));
                        ctx.SetOutput("Found", true);
                        return;
                    }
                }
                ctx.SetOutput("Value", null!);
                ctx.SetOutput("Found", false);
            }).Build();

        yield return NodeBuilder.Create("JSON Array Length")
            .Category("JSON").Description("Returns the length of a JSON array.")
            .Input<object>("Json").Output<int>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var jsonObj = ctx.GetInput<object>("Json");
                if (jsonObj is JsonElement el && el.ValueKind == JsonValueKind.Array)
                    ctx.SetOutput("Result", el.GetArrayLength());
                else
                    ctx.SetOutput("Result", 0);
            }).Build();

        yield return NodeBuilder.Create("JSON To Dict")
            .Category("JSON").Description("Converts a JSON object to a SerializableDict.")
            .Input<object>("Json").Output<SerializableDict>("Result").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                var jsonObj = ctx.GetInput<object>("Json");
                if (jsonObj is JsonElement el && el.ValueKind == JsonValueKind.Object)
                {
                    var dict = new SerializableDict();
                    foreach (var prop in el.EnumerateObject())
                        dict.Set(prop.Name, UnwrapJsonElement(prop.Value));
                    ctx.SetOutput("Result", dict);
                    ctx.SetOutput("Success", true);
                }
                else
                {
                    ctx.SetOutput("Result", new SerializableDict());
                    ctx.SetOutput("Success", false);
                }
            }).Build();

        yield return NodeBuilder.Create("JSON To List")
            .Category("JSON").Description("Converts a JSON array to a SerializableList.")
            .Input<object>("Json").Output<SerializableList>("Result").Output<bool>("Success")
            .OnExecute(async (ctx, ct) =>
            {
                var jsonObj = ctx.GetInput<object>("Json");
                if (jsonObj is JsonElement el && el.ValueKind == JsonValueKind.Array)
                {
                    var list = new SerializableList();
                    foreach (var item in el.EnumerateArray())
                        list.Add(UnwrapJsonElement(item));
                    ctx.SetOutput("Result", list);
                    ctx.SetOutput("Success", true);
                }
                else
                {
                    ctx.SetOutput("Result", new SerializableList());
                    ctx.SetOutput("Success", false);
                }
            }).Build();

        yield return NodeBuilder.Create("JSON Path")
            .Category("JSON").Description("Navigates a dot-separated path (e.g. 'a.b.c' or 'arr.0') into a JSON element.")
            .Input<object>("Json").Input<string>("Path", "").Output<object>("Value").Output<bool>("Found")
            .OnExecute(async (ctx, ct) =>
            {
                var jsonObj = ctx.GetInput<object>("Json");
                var path = ctx.GetInput<string>("Path") ?? "";
                if (jsonObj is not JsonElement current)
                {
                    ctx.SetOutput("Value", null!);
                    ctx.SetOutput("Found", false);
                    return;
                }

                var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
                foreach (var segment in segments)
                {
                    if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var prop))
                    {
                        current = prop;
                    }
                    else if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var idx))
                    {
                        if (idx >= 0 && idx < current.GetArrayLength())
                            current = current[idx];
                        else
                        {
                            ctx.SetOutput("Value", null!);
                            ctx.SetOutput("Found", false);
                            return;
                        }
                    }
                    else
                    {
                        ctx.SetOutput("Value", null!);
                        ctx.SetOutput("Found", false);
                        return;
                    }
                }

                ctx.SetOutput("Value", (object)UnwrapJsonElement(current));
                ctx.SetOutput("Found", true);
            }).Build();
    }

    private static object UnwrapJsonElement(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString()!,
        JsonValueKind.Number when el.TryGetInt32(out var i) => i,
        JsonValueKind.Number => el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null!,
        _ => (object)el.Clone()
    };
}
