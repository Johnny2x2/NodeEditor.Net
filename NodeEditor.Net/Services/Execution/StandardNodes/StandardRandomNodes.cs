using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardRandomNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("Random Double")
            .Category("Random").Description("Random double between 0.0 and 1.0.")
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Random.Shared.NextDouble()))
            .Build();

        yield return NodeBuilder.Create("Random Double Range")
            .Category("Random").Description("Random double between Min and Max.")
            .Input<double>("Min", 0.0).Input<double>("Max", 1.0).Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var min = ctx.GetInput<double>("Min");
                var max = ctx.GetInput<double>("Max");
                ctx.SetOutput("Result", min + Random.Shared.NextDouble() * (max - min));
            }).Build();

        yield return NodeBuilder.Create("Random Bool")
            .Category("Random").Description("Random boolean (coin flip).")
            .Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Random.Shared.Next(2) == 1))
            .Build();

        yield return NodeBuilder.Create("Random Bool Weighted")
            .Category("Random").Description("Random boolean with a probability (0.0–1.0) of being true.")
            .Input<double>("Probability", 0.5).Output<bool>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Random.Shared.NextDouble() < ctx.GetInput<double>("Probability")))
            .Build();

        yield return NodeBuilder.Create("Random Choice")
            .Category("Random").Description("Picks a random item from a list.")
            .Input<SerializableList>("List").Output<object>("Result").Output<int>("Index")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                if (snapshot.Length == 0) { ctx.SetOutput("Result", null!); ctx.SetOutput("Index", -1); return; }
                var idx = Random.Shared.Next(snapshot.Length);
                ctx.SetOutput("Result", snapshot[idx]);
                ctx.SetOutput("Index", idx);
            }).Build();

        yield return NodeBuilder.Create("Random Shuffle")
            .Category("Random").Description("Returns a randomly shuffled copy of a list.")
            .Input<SerializableList>("List").Output<SerializableList>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var snapshot = ctx.GetInput<SerializableList>("List")?.Snapshot() ?? Array.Empty<object>();
                var shuffled = snapshot.ToArray();
                Random.Shared.Shuffle(shuffled);
                var result = new SerializableList();
                foreach (var item in shuffled) result.Add(item);
                ctx.SetOutput("Result", result);
            }).Build();

        yield return NodeBuilder.Create("Random String")
            .Category("Random").Description("Generates a random alphanumeric string of the given length.")
            .Input<int>("Length", 8).Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
                var length = Math.Max(0, Math.Min(ctx.GetInput<int>("Length"), 10_000));
                var buffer = new char[length];
                for (int i = 0; i < length; i++)
                    buffer[i] = chars[Random.Shared.Next(chars.Length)];
                ctx.SetOutput("Result", new string(buffer));
            }).Build();

        yield return NodeBuilder.Create("Random GUID")
            .Category("Random").Description("Generates a new random GUID string.")
            .Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", Guid.NewGuid().ToString()))
            .Build();
    }
}
