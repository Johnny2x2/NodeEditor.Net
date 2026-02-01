using System.Text.Json;
using Microsoft.Maui.Storage;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Serialization;

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
        await AddSampleGraphsAsync().ConfigureAwait(false);

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

    private async Task AddSampleGraphsAsync()
    {
        await AddSampleGraphFromPackageAsync(
            "Loop + Lists + Strings",
            "graphs/loop-list-string.json",
            fallback: null).ConfigureAwait(false);

        await AddSampleGraphFromPackageAsync(
            "Parallel Split/Join",
            "graphs/parallel.json",
            fallback: null).ConfigureAwait(false);

        await AddSampleGraphFromPackageAsync(
            "LLM Tornado Message Demo",
            "graphs/llmtornado-demo.json",
            fallback: null).ConfigureAwait(false);

        await AddSampleGraphFromPackageAsync(
            "LLM Tornado Orchestration Demo",
            "graphs/llmtornado-agent-orchestration.json",
            fallback: null).ConfigureAwait(false);
    }

    private async Task AddSampleGraphFromPackageAsync(string name, string path, string? fallback)
    {
        try
        {
            using var stream = await FileSystem.OpenAppPackageFileAsync(path).ConfigureAwait(false);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync().ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(json))
            {
                _items.Add(new GraphLibraryItem(name, json, IsSample: true));
                return;
            }
        }
        catch
        {
            // Ignore and fall back if provided.
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            _items.Add(new GraphLibraryItem(name, fallback, IsSample: true));
        }
    }

}

public sealed record GraphLibraryItem(string Name, string Json, bool IsSample);

public sealed record GraphLibraryEntry(string Name, string Json);

public sealed record GraphLibraryStore(List<GraphLibraryEntry> Graphs);
