using System.Globalization;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

public static class StandardDateTimeNodes
{
    public static IEnumerable<NodeDefinition> GetDefinitions()
    {
        yield return NodeBuilder.Create("Now")
            .Category("DateTime").Description("Gets the current date and time as a string.")
            .Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", DateTime.Now.ToString("o", CultureInfo.InvariantCulture)))
            .Build();

        yield return NodeBuilder.Create("UTC Now")
            .Category("DateTime").Description("Gets the current UTC date and time as a string.")
            .Output<string>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)))
            .Build();

        yield return NodeBuilder.Create("Format DateTime")
            .Category("DateTime").Description("Formats a date/time string. E.g. yyyy-MM-dd HH:mm:ss.")
            .Input<string>("DateTime", "").Input<string>("Format", "yyyy-MM-dd HH:mm:ss").Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var dtStr = ctx.GetInput<string>("DateTime") ?? "";
                var format = ctx.GetInput<string>("Format") ?? "yyyy-MM-dd HH:mm:ss";
                if (DateTime.TryParse(dtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    ctx.SetOutput("Result", dt.ToString(format, CultureInfo.InvariantCulture));
                else
                    ctx.SetOutput("Result", dtStr);
            }).Build();

        yield return NodeBuilder.Create("Timestamp")
            .Category("DateTime").Description("Gets the current Unix timestamp in seconds.")
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (double)DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            .Build();

        yield return NodeBuilder.Create("Timestamp Ms")
            .Category("DateTime").Description("Gets the current Unix timestamp in milliseconds.")
            .Output<double>("Result")
            .OnExecute(async (ctx, ct) => ctx.SetOutput("Result", (double)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()))
            .Build();

        yield return NodeBuilder.Create("Add Days")
            .Category("DateTime").Description("Adds days to a date/time string.")
            .Input<string>("DateTime", "").Input<double>("Days", 1.0).Output<string>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var dtStr = ctx.GetInput<string>("DateTime") ?? "";
                if (DateTime.TryParse(dtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                    ctx.SetOutput("Result", dt.AddDays(ctx.GetInput<double>("Days")).ToString("o", CultureInfo.InvariantCulture));
                else
                    ctx.SetOutput("Result", dtStr);
            }).Build();

        yield return NodeBuilder.Create("Date Diff Days")
            .Category("DateTime").Description("Returns the number of days between two date/time strings.")
            .Input<string>("From", "").Input<string>("To", "").Output<double>("Result")
            .OnExecute(async (ctx, ct) =>
            {
                var fromStr = ctx.GetInput<string>("From") ?? "";
                var toStr = ctx.GetInput<string>("To") ?? "";
                if (DateTime.TryParse(fromStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var from)
                    && DateTime.TryParse(toStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var to))
                    ctx.SetOutput("Result", (to - from).TotalDays);
                else
                    ctx.SetOutput("Result", 0.0);
            }).Build();

        yield return NodeBuilder.Create("Parse Date Parts")
            .Category("DateTime").Description("Extracts year, month, day, hour, minute, second from a date/time string.")
            .Input<string>("DateTime", "")
            .Output<int>("Year").Output<int>("Month").Output<int>("Day")
            .Output<int>("Hour").Output<int>("Minute").Output<int>("Second")
            .OnExecute(async (ctx, ct) =>
            {
                var dtStr = ctx.GetInput<string>("DateTime") ?? "";
                if (DateTime.TryParse(dtStr, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                {
                    ctx.SetOutput("Year", dt.Year);
                    ctx.SetOutput("Month", dt.Month);
                    ctx.SetOutput("Day", dt.Day);
                    ctx.SetOutput("Hour", dt.Hour);
                    ctx.SetOutput("Minute", dt.Minute);
                    ctx.SetOutput("Second", dt.Second);
                }
                else
                {
                    ctx.SetOutput("Year", 0); ctx.SetOutput("Month", 0); ctx.SetOutput("Day", 0);
                    ctx.SetOutput("Hour", 0); ctx.SetOutput("Minute", 0); ctx.SetOutput("Second", 0);
                }
            }).Build();
    }
}
