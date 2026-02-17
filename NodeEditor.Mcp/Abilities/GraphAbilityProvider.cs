using System.Text.Json;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Serialization;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for saving, loading, and managing graphs.
/// </summary>
public sealed class GraphAbilityProvider : IAbilityProvider
{
    private readonly INodeEditorState _state;
    private readonly IGraphSerializer _serializer;

    public GraphAbilityProvider(INodeEditorState state, IGraphSerializer serializer)
    {
        _state = state;
        _serializer = serializer;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("graph.save", "Save Graph", "Graph",
            "Saves the current graph to a JSON file.",
            "Provide a file path. The graph is exported as JSON including all nodes, connections, variables, events, and overlays.",
            [new("filePath", "string", "Absolute file path to save the graph to.")]),

        new("graph.load", "Load Graph", "Graph",
            "Loads a graph from a JSON file, replacing the current canvas.",
            "Provide the file path of a previously saved graph. The current canvas will be cleared and replaced.",
            [new("filePath", "string", "Absolute file path of the graph to load.")]),

        new("graph.export_json", "Export Graph as JSON", "Graph",
            "Exports the current graph as a JSON string (without saving to disk).",
            "Useful for inspecting the full graph structure or sending to another system.",
            [],
            ReturnDescription: "The graph JSON string."),

        new("graph.import_json", "Import Graph from JSON", "Graph",
            "Imports a graph from a JSON string, replacing the current canvas.",
            "Provide the JSON string of a graph to load.",
            [new("json", "string", "The graph JSON string to import.")]),

        new("graph.clear", "Clear Graph", "Graph",
            "Clears all nodes, connections, overlays, and variables from the canvas.",
            "This cannot be undone. All current graph data will be lost.",
            []),

        new("graph.summary", "Graph Summary", "Graph",
            "Returns a summary of the current graph state.",
            "Shows counts of nodes, connections, variables, events, and overlays.",
            [],
            ReturnDescription: "Summary object with counts and overview."),

        new("graph.variable_list", "List Variables", "Graph",
            "Lists all graph-level variables.",
            "Returns variable names, types, and current values.",
            []),

        new("graph.variable_add", "Add Variable", "Graph",
            "Adds a new graph-level variable.",
            "Provide name, type, and optional default value.",
            [
                new("name", "string", "Variable name."),
                new("type", "string", "Variable type (e.g. 'String', 'Int32', 'Boolean')."),
                new("defaultValue", "string", "Default value as string.", Required: false, DefaultValue: "")
            ]),

        new("graph.variable_remove", "Remove Variable", "Graph",
            "Removes a graph-level variable.",
            "Provide the variable ID to remove.",
            [new("variableId", "string", "The ID of the variable to remove.")]),

        new("graph.event_list", "List Events", "Graph",
            "Lists all graph-level events.",
            "Returns event names and details.",
            []),

        new("graph.event_add", "Add Event", "Graph",
            "Adds a new graph-level event.",
            "Provide a name for the event.",
            [new("name", "string", "Event name.")]),

        new("graph.event_remove", "Remove Event", "Graph",
            "Removes a graph-level event.",
            "Provide the event ID to remove.",
            [new("eventId", "string", "The ID of the event to remove.")])
    ];

    public async Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return abilityId switch
        {
            "graph.save" => await SaveGraph(parameters),
            "graph.load" => await LoadGraph(parameters),
            "graph.export_json" => ExportJson(),
            "graph.import_json" => ImportJson(parameters),
            "graph.clear" => ClearGraph(),
            "graph.summary" => GraphSummary(),
            "graph.variable_list" => ListVariables(),
            "graph.variable_add" => AddVariable(parameters),
            "graph.variable_remove" => RemoveVariable(parameters),
            "graph.event_list" => ListEvents(),
            "graph.event_add" => AddEvent(parameters),
            "graph.event_remove" => RemoveEvent(parameters),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        };
    }

    private async Task<AbilityResult> SaveGraph(JsonElement p)
    {
        if (!p.TryGetProperty("filePath", out var pathEl))
            return new AbilityResult(false, "Missing required parameter 'filePath'.");

        var filePath = pathEl.GetString()!;
        try
        {
            var graphData = _state.ExportToGraphData();
            var json = _serializer.SerializeGraphData(graphData);
            await File.WriteAllTextAsync(filePath, json);
            return new AbilityResult(true, $"Graph saved to '{filePath}'.");
        }
        catch (Exception ex)
        {
            return new AbilityResult(false, $"Failed to save graph: {ex.Message}");
        }
    }

    private async Task<AbilityResult> LoadGraph(JsonElement p)
    {
        if (!p.TryGetProperty("filePath", out var pathEl))
            return new AbilityResult(false, "Missing required parameter 'filePath'.");

        var filePath = pathEl.GetString()!;
        if (!File.Exists(filePath))
            return new AbilityResult(false, $"File not found: '{filePath}'.");

        try
        {
            var json = await File.ReadAllTextAsync(filePath);
            var graphData = _serializer.DeserializeToGraphData(json);
            _state.LoadFromGraphData(graphData);
            return new AbilityResult(true, $"Graph loaded from '{filePath}'.",
                Data: new
                {
                    Nodes = _state.Nodes.Count,
                    Connections = _state.Connections.Count
                });
        }
        catch (Exception ex)
        {
            return new AbilityResult(false, $"Failed to load graph: {ex.Message}");
        }
    }

    private AbilityResult ExportJson()
    {
        var graphData = _state.ExportToGraphData();
        var json = _serializer.SerializeGraphData(graphData);
        return new AbilityResult(true, "Graph exported as JSON.", Data: json);
    }

    private AbilityResult ImportJson(JsonElement p)
    {
        if (!p.TryGetProperty("json", out var jsonEl))
            return new AbilityResult(false, "Missing required parameter 'json'.");

        try
        {
            var json = jsonEl.GetString()!;
            var graphData = _serializer.DeserializeToGraphData(json);
            _state.LoadFromGraphData(graphData);
            return new AbilityResult(true, "Graph imported successfully.",
                Data: new
                {
                    Nodes = _state.Nodes.Count,
                    Connections = _state.Connections.Count
                });
        }
        catch (Exception ex)
        {
            return new AbilityResult(false, $"Failed to import graph: {ex.Message}");
        }
    }

    private AbilityResult ClearGraph()
    {
        _state.Clear();
        return new AbilityResult(true, "Graph cleared.");
    }

    private AbilityResult GraphSummary()
    {
        return new AbilityResult(true, Data: new
        {
            NodeCount = _state.Nodes.Count,
            ConnectionCount = _state.Connections.Count,
            VariableCount = _state.Variables.Count,
            EventCount = _state.Events.Count,
            OverlayCount = _state.Overlays.Count,
            SelectedNodeCount = _state.SelectedNodeIds.Count,
            Nodes = _state.Nodes.Select(n => new { n.Data.Id, n.Data.Name }).ToList()
        });
    }

    private AbilityResult ListVariables()
    {
        var variables = _state.Variables.Select(v => new
        {
            v.Id,
            v.Name,
            v.TypeName,
            v.DefaultValue
        }).ToList();

        return new AbilityResult(true, $"Found {variables.Count} variable(s).", Data: variables);
    }

    private AbilityResult AddVariable(JsonElement p)
    {
        if (!p.TryGetProperty("name", out var nameEl))
            return new AbilityResult(false, "Missing required parameter 'name'.");
        if (!p.TryGetProperty("type", out var typeEl))
            return new AbilityResult(false, "Missing required parameter 'type'.");

        var defaultValue = p.TryGetProperty("defaultValue", out var defEl) && defEl.GetString() is string dv
            ? SocketValue.FromObject(dv)
            : null;

        var variable = new GraphVariable(
            Id: Guid.NewGuid().ToString("N"),
            Name: nameEl.GetString()!,
            TypeName: typeEl.GetString()!,
            DefaultValue: defaultValue);

        _state.AddVariable(variable);
        return new AbilityResult(true, $"Variable '{variable.Name}' added.", Data: new { variable.Id, variable.Name });
    }

    private AbilityResult RemoveVariable(JsonElement p)
    {
        if (!p.TryGetProperty("variableId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'variableId'.");

        _state.RemoveVariable(idEl.GetString()!);
        return new AbilityResult(true, "Variable removed.");
    }

    private AbilityResult ListEvents()
    {
        var events = _state.Events.Select(e => new
        {
            e.Id,
            e.Name
        }).ToList();

        return new AbilityResult(true, $"Found {events.Count} event(s).", Data: events);
    }

    private AbilityResult AddEvent(JsonElement p)
    {
        if (!p.TryGetProperty("name", out var nameEl))
            return new AbilityResult(false, "Missing required parameter 'name'.");

        var evt = new GraphEvent(
            Id: Guid.NewGuid().ToString("N"),
            Name: nameEl.GetString()!);

        _state.AddEvent(evt);
        return new AbilityResult(true, $"Event '{evt.Name}' added.", Data: new { evt.Id, evt.Name });
    }

    private AbilityResult RemoveEvent(JsonElement p)
    {
        if (!p.TryGetProperty("eventId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'eventId'.");

        _state.RemoveEvent(idEl.GetString()!);
        return new AbilityResult(true, "Event removed.");
    }
}
