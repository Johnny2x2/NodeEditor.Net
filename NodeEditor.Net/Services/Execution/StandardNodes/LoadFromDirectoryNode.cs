using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution.StandardNodes;

/// <summary>
/// Enumerates files in a directory with optional wildcard filtering.
/// Supports * and ? wildcards in the filter (e.g., "*.txt", "image*.*").
/// Outputs a <see cref="SerializableList"/> of full file path strings.
/// </summary>
public sealed class LoadFromDirectoryNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder
            .Name("Load From Directory").Category("IO")
            .Description("Lists files in a directory. Filter supports * and ? wildcards (e.g., \"*.txt\", \"photo*\"). Output is a list of full file paths.")
            .Callable()
            .Input<string>("DirectoryPath", "")
            .Input<string>("Filter", "*")
            .Input<bool>("Recursive", false)
            .Output<SerializableList>("Files")
            .Output<int>("Count")
            .Output<bool>("Ok")
            .Output<string>("Error");
    }

    public override async Task ExecuteAsync(INodeExecutionContext context, CancellationToken ct)
    {
        var directoryPath = context.GetInput<string>("DirectoryPath");
        var filter = context.GetInput<string>("Filter");
        var recursive = context.GetInput<bool>("Recursive");

        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                SetError(context, "DirectoryPath is required.");
                await context.TriggerAsync("Exit");
                return;
            }

            if (string.IsNullOrWhiteSpace(filter))
            {
                filter = "*";
            }

            if (!Directory.Exists(directoryPath))
            {
                SetError(context, $"Directory not found: {directoryPath}");
                await context.TriggerAsync("Exit");
                return;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = new SerializableList();

            // Enumerate files. Directory.EnumerateFiles natively supports *, ? wildcards.
            foreach (var file in Directory.EnumerateFiles(directoryPath, filter, searchOption))
            {
                ct.ThrowIfCancellationRequested();
                files.Add(file);
            }

            context.SetOutput("Files", files);
            context.SetOutput("Count", files.Count);
            context.SetOutput("Ok", true);
            context.SetOutput("Error", string.Empty);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            SetError(context, ex.Message);
        }

        await context.TriggerAsync("Exit");
    }

    private static void SetError(INodeExecutionContext context, string message)
    {
        context.SetOutput("Files", new SerializableList());
        context.SetOutput("Count", 0);
        context.SetOutput("Ok", false);
        context.SetOutput("Error", message);
    }
}
