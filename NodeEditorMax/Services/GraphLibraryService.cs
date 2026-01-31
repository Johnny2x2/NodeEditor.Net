using System.Text.Json;
using Microsoft.Maui.Storage;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Serialization;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditorMax.Services;

public sealed class GraphLibraryService
{
    private const string StorageFileName = "graph-library.json";
    private readonly GraphSerializer _serializer;
    private readonly string _storagePath;
    private readonly List<GraphLibraryItem> _items = new();
    private bool _initialized;

    public GraphLibraryService(GraphSerializer serializer)
    {
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _storagePath = Path.Combine(FileSystem.Current.AppDataDirectory, StorageFileName);
    }

    public IReadOnlyList<GraphLibraryItem> Items => _items;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        var userEntries = await LoadUserEntriesAsync().ConfigureAwait(false);

        _items.Clear();
        AddSampleGraphs();

        foreach (var entry in userEntries)
        {
            if (_items.Any(item => item.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _items.Add(new GraphLibraryItem(entry.Name, entry.Json, IsSample: false));
        }
    }

    public Task<GraphImportResult> LoadGraphAsync(string name, NodeEditorState state)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Graph library has not been initialized.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Graph name is required.", nameof(name));
        }

        var item = _items.FirstOrDefault(candidate =>
            candidate.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            throw new InvalidOperationException($"Graph '{name}' was not found.");
        }

        var dto = _serializer.Deserialize(item.Json);
        var result = _serializer.Import(state, dto);
        return Task.FromResult(result);
    }

    public async Task SaveGraphAsync(string name, NodeEditorState state)
    {
        if (!_initialized)
        {
            throw new InvalidOperationException("Graph library has not been initialized.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Graph name is required.", nameof(name));
        }

        if (_items.Any(item => item.IsSample &&
                item.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Cannot overwrite built-in example graphs.");
        }

        var json = _serializer.Serialize(_serializer.Export(state));
        var existingIndex = _items.FindIndex(item =>
            !item.IsSample && item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            _items[existingIndex] = new GraphLibraryItem(name, json, IsSample: false);
        }
        else
        {
            _items.Add(new GraphLibraryItem(name, json, IsSample: false));
        }

        await SaveUserEntriesAsync().ConfigureAwait(false);
    }

    private async Task<List<GraphLibraryEntry>> LoadUserEntriesAsync()
    {
        if (!File.Exists(_storagePath))
        {
            return new List<GraphLibraryEntry>();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_storagePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<GraphLibraryEntry>();
            }

            var store = JsonSerializer.Deserialize<GraphLibraryStore>(json);
            return store?.Graphs ?? new List<GraphLibraryEntry>();
        }
        catch
        {
            return new List<GraphLibraryEntry>();
        }
    }

    private async Task SaveUserEntriesAsync()
    {
        var store = new GraphLibraryStore(
            _items
                .Where(item => !item.IsSample)
                .Select(item => new GraphLibraryEntry(item.Name, item.Json))
                .ToList());

        var json = JsonSerializer.Serialize(store, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(_storagePath, json).ConfigureAwait(false);
    }

    private void AddSampleGraphs()
    {
        _items.Add(new GraphLibraryItem(
            "Example: Sum and Print",
            BuildSumGraphJson(),
            IsSample: true));

        _items.Add(new GraphLibraryItem(
            "Example: Single Value",
            BuildSingleValueGraphJson(),
            IsSample: true));
    }

    private string BuildSumGraphJson()
    {
        var state = new NodeEditorState();

        var startNode = new NodeViewModel(new NodeData(
            Id: "example-sum-start",
            Name: "Start",
            Callable: true,
            ExecInit: true,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[]
            {
                new SocketData("ExecOut", "execution", false, true)
            }))
        {
            Position = new Point2D(40, 90)
        };

        var valueANode = new NodeViewModel(new NodeData(
            Id: "example-sum-value-a",
            Name: "Value",
            Callable: false,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("Value", "int", true, false)
            },
            Outputs: new[]
            {
                new SocketData("Value_Out", "int", false, false)
            }))
        {
            Position = new Point2D(210, 10)
        };

        var valueBNode = new NodeViewModel(new NodeData(
            Id: "example-sum-value-b",
            Name: "Value",
            Callable: false,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("Value", "int", true, false)
            },
            Outputs: new[]
            {
                new SocketData("Value_Out", "int", false, false)
            }))
        {
            Position = new Point2D(210, 120)
        };

        var addNode = new NodeViewModel(new NodeData(
            Id: "example-sum-add",
            Name: "Add",
            Callable: false,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("A", "int", true, false),
                new SocketData("B", "int", true, false)
            },
            Outputs: new[]
            {
                new SocketData("Result", "int", false, false)
            }))
        {
            Position = new Point2D(470, 70)
        };

        var printNode = new NodeViewModel(new NodeData(
            Id: "example-sum-print",
            Name: "Print",
            Callable: true,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("ExecIn", "execution", true, true),
                new SocketData("Message", "string", true, false)
            },
            Outputs: new[]
            {
                new SocketData("ExecOut", "execution", false, true)
            }))
        {
            Position = new Point2D(300, 210)
        };

        var toStringNode = new NodeViewModel(new NodeData(
            Id: "example-sum-tostring",
            Name: "ToString",
            Callable: false,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("Input", "object", true, false)
            },
            Outputs: new[]
            {
                new SocketData("Output", "string", false, false)
            }))
        {
            Position = new Point2D(570, 100)
        };

        state.AddNode(startNode);
        state.AddNode(valueANode);
        state.AddNode(valueBNode);
        state.AddNode(addNode);
        state.AddNode(printNode);
        state.AddNode(toStringNode);

        state.AddConnection(new ConnectionData("example-sum-start", "example-sum-print", "ExecOut", "ExecIn", true));
        state.AddConnection(new ConnectionData("example-sum-value-a", "example-sum-add", "Value_Out", "A", false));
        state.AddConnection(new ConnectionData("example-sum-value-b", "example-sum-add", "Value_Out", "B", false));
        state.AddConnection(new ConnectionData("example-sum-add", "example-sum-tostring", "Result", "Input", false));
        state.AddConnection(new ConnectionData("example-sum-tostring", "example-sum-print", "Output", "Message", false));

        return _serializer.Serialize(_serializer.Export(state));
    }

    private string BuildSingleValueGraphJson()
    {
        var state = new NodeEditorState();

        var startNode = new NodeViewModel(new NodeData(
            Id: "example-single-start",
            Name: "Start",
            Callable: true,
            ExecInit: true,
            Inputs: Array.Empty<SocketData>(),
            Outputs: new[]
            {
                new SocketData("ExecOut", "execution", false, true)
            }))
        {
            Position = new Point2D(40, 80)
        };

        var valueNode = new NodeViewModel(new NodeData(
            Id: "example-single-value",
            Name: "Value",
            Callable: false,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("Value", "int", true, false)
            },
            Outputs: new[]
            {
                new SocketData("Value_Out", "int", false, false)
            }))
        {
            Position = new Point2D(220, 120)
        };

        var toStringNode = new NodeViewModel(new NodeData(
            Id: "example-single-tostring",
            Name: "ToString",
            Callable: false,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("Input", "object", true, false)
            },
            Outputs: new[]
            {
                new SocketData("Output", "string", false, false)
            }))
        {
            Position = new Point2D(440, 120)
        };

        var printNode = new NodeViewModel(new NodeData(
            Id: "example-single-print",
            Name: "Print",
            Callable: true,
            ExecInit: false,
            Inputs: new[]
            {
                new SocketData("ExecIn", "execution", true, true),
                new SocketData("Message", "string", true, false)
            },
            Outputs: new[]
            {
                new SocketData("ExecOut", "execution", false, true)
            }))
        {
            Position = new Point2D(300, 220)
        };

        state.AddNode(startNode);
        state.AddNode(valueNode);
        state.AddNode(toStringNode);
        state.AddNode(printNode);

        state.AddConnection(new ConnectionData("example-single-start", "example-single-print", "ExecOut", "ExecIn", true));
        state.AddConnection(new ConnectionData("example-single-value", "example-single-tostring", "Value_Out", "Input", false));
        state.AddConnection(new ConnectionData("example-single-tostring", "example-single-print", "Output", "Message", false));

        return _serializer.Serialize(_serializer.Export(state));
    }
}

public sealed record GraphLibraryItem(string Name, string Json, bool IsSample);

public sealed record GraphLibraryEntry(string Name, string Json);

public sealed record GraphLibraryStore(List<GraphLibraryEntry> Graphs);
