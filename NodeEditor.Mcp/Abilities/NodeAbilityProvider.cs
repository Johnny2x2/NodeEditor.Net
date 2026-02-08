using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.Services.Registry;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for managing nodes on the canvas.
/// </summary>
public sealed class NodeAbilityProvider : IAbilityProvider
{
    private readonly INodeEditorState _state;
    private readonly INodeRegistryService _registry;

    public NodeAbilityProvider(INodeEditorState state, INodeRegistryService registry)
    {
        _state = state;
        _registry = registry;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("node.add", "Add Node", "Nodes",
            "Creates a new node on the canvas from a node definition.",
            "Provide the definitionId (from the node catalog) and optional position. " +
            "Use catalog.list or catalog.search to discover available definitions. " +
            "Returns the new node's id.",
            [
                new("definitionId", "string", "The definition ID from the node catalog.", Required: true),
                new("x", "number", "X position on canvas.", Required: false, DefaultValue: "0"),
                new("y", "number", "Y position on canvas.", Required: false, DefaultValue: "0")
            ],
            ReturnDescription: "The created node's id and name."),

        new("node.remove", "Remove Node", "Nodes",
            "Removes a node and its connections from the canvas.",
            "Provide the nodeId of the node to remove. All connections to/from the node are also removed.",
            [new("nodeId", "string", "The ID of the node to remove.")]),

        new("node.list", "List Nodes", "Nodes",
            "Lists all nodes currently on the canvas with their positions and socket details.",
            "Returns all nodes with their id, name, position, inputs, outputs, and connection status.",
            [],
            ReturnDescription: "Array of node objects with id, name, position, inputs, and outputs."),

        new("node.get", "Get Node Details", "Nodes",
            "Gets detailed information about a specific node.",
            "Provide the nodeId to get full details including socket values.",
            [new("nodeId", "string", "The ID of the node.")],
            ReturnDescription: "Full node details including sockets and their current values."),

        new("node.move", "Move Node", "Nodes",
            "Moves a node to a new position on the canvas.",
            "Provide the nodeId and the new x,y coordinates.",
            [
                new("nodeId", "string", "The ID of the node to move."),
                new("x", "number", "New X position."),
                new("y", "number", "New Y position.")
            ]),

        new("node.select", "Select Nodes", "Nodes",
            "Selects one or more nodes on the canvas.",
            "Provide a list of node IDs to select. Use clearExisting to control whether existing selection is cleared first.",
            [
                new("nodeIds", "string[]", "Array of node IDs to select."),
                new("clearExisting", "boolean", "Clear existing selection first.", Required: false, DefaultValue: "true")
            ]),

        new("node.select_all", "Select All Nodes", "Nodes",
            "Selects all nodes on the canvas.",
            "No parameters required.",
            []),

        new("node.clear_selection", "Clear Node Selection", "Nodes",
            "Clears the current node selection.",
            "No parameters required.",
            []),

        new("node.remove_selected", "Remove Selected Nodes", "Nodes",
            "Removes all currently selected nodes and their connections.",
            "No parameters required.",
            []),

        new("node.set_socket_value", "Set Socket Value", "Nodes",
            "Sets the value of a socket on a node.",
            "Provide the nodeId, socketName, and value. The socket must be an input socket.",
            [
                new("nodeId", "string", "The ID of the node."),
                new("socketName", "string", "The name of the input socket."),
                new("value", "string", "The value to set (will be parsed to the socket's type).")
            ])
    ];

    public Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(abilityId switch
        {
            "node.add" => AddNode(parameters),
            "node.remove" => RemoveNode(parameters),
            "node.list" => ListNodes(),
            "node.get" => GetNode(parameters),
            "node.move" => MoveNode(parameters),
            "node.select" => SelectNodes(parameters),
            "node.select_all" => SelectAllNodes(),
            "node.clear_selection" => ClearSelection(),
            "node.remove_selected" => RemoveSelected(),
            "node.set_socket_value" => SetSocketValue(parameters),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        });
    }

    private AbilityResult AddNode(JsonElement p)
    {
        if (!p.TryGetProperty("definitionId", out var defIdEl))
            return new AbilityResult(false, "Missing required parameter 'definitionId'.",
                ErrorHint: "Use catalog.list or catalog.search to find available definitionIds.");

        var defId = defIdEl.GetString()!;
        _registry.EnsureInitialized();
        var def = _registry.Definitions.FirstOrDefault(d =>
            d.Id.Equals(defId, StringComparison.OrdinalIgnoreCase));

        if (def is null)
            return new AbilityResult(false, $"Definition '{defId}' not found.",
                ErrorHint: "Use catalog.list to see available node definitions.");

        var nodeData = def.Factory();
        var vm = new NodeViewModel(nodeData);

        var x = p.TryGetProperty("x", out var xEl) ? xEl.GetDouble() : 0;
        var y = p.TryGetProperty("y", out var yEl) ? yEl.GetDouble() : 0;
        vm.Position = new Point2D(x, y);

        _state.AddNode(vm);

        return new AbilityResult(true, $"Node '{nodeData.Name}' created.",
            Data: new { nodeData.Id, nodeData.Name, Position = new { x, y } });
    }

    private AbilityResult RemoveNode(JsonElement p)
    {
        if (!p.TryGetProperty("nodeId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'nodeId'.");

        var nodeId = idEl.GetString()!;
        var node = _state.Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
            return new AbilityResult(false, $"Node '{nodeId}' not found.",
                ErrorHint: "Use node.list to see existing nodes.");

        _state.RemoveConnectionsToNode(nodeId);
        _state.RemoveNode(nodeId);
        return new AbilityResult(true, $"Node '{node.Data.Name}' removed.");
    }

    private AbilityResult ListNodes()
    {
        var nodes = _state.Nodes.Select(n => new
        {
            n.Data.Id,
            n.Data.Name,
            Position = new { X = n.Position.X, Y = n.Position.Y },
            n.IsSelected,
            Inputs = n.Inputs.Select(s => new { s.Data.Name, s.Data.TypeName, s.Data.IsExecution, Value = s.Data.Value?.ToString() }).ToList(),
            Outputs = n.Outputs.Select(s => new { s.Data.Name, s.Data.TypeName, s.Data.IsExecution }).ToList()
        }).ToList();

        return new AbilityResult(true, $"Found {nodes.Count} node(s).", Data: nodes);
    }

    private AbilityResult GetNode(JsonElement p)
    {
        if (!p.TryGetProperty("nodeId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'nodeId'.");

        var nodeId = idEl.GetString()!;
        var node = _state.Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
            return new AbilityResult(false, $"Node '{nodeId}' not found.");

        var connections = _state.Connections
            .Where(c => c.OutputNodeId == nodeId || c.InputNodeId == nodeId)
            .Select(c => new { c.OutputNodeId, c.InputNodeId, c.OutputSocketName, c.InputSocketName, c.IsExecution })
            .ToList();

        return new AbilityResult(true, Data: new
        {
            node.Data.Id,
            node.Data.Name,
            node.Data.Callable,
            node.Data.DefinitionId,
            Position = new { X = node.Position.X, Y = node.Position.Y },
            Size = new { Width = node.Size.Width, Height = node.Size.Height },
            node.IsSelected,
            node.IsExecuting,
            node.IsError,
            Inputs = node.Inputs.Select(s => new { s.Data.Name, s.Data.TypeName, s.Data.IsExecution, Value = s.Data.Value?.ToString() }).ToList(),
            Outputs = node.Outputs.Select(s => new { s.Data.Name, s.Data.TypeName, s.Data.IsExecution }).ToList(),
            Connections = connections
        });
    }

    private AbilityResult MoveNode(JsonElement p)
    {
        if (!p.TryGetProperty("nodeId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'nodeId'.");
        if (!p.TryGetProperty("x", out var xEl) || !p.TryGetProperty("y", out var yEl))
            return new AbilityResult(false, "Missing required parameters 'x' and 'y'.");

        var nodeId = idEl.GetString()!;
        var node = _state.Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
            return new AbilityResult(false, $"Node '{nodeId}' not found.");

        node.Position = new Point2D(xEl.GetDouble(), yEl.GetDouble());
        return new AbilityResult(true, $"Node '{node.Data.Name}' moved to ({xEl.GetDouble()}, {yEl.GetDouble()}).");
    }

    private AbilityResult SelectNodes(JsonElement p)
    {
        if (!p.TryGetProperty("nodeIds", out var idsEl))
            return new AbilityResult(false, "Missing required parameter 'nodeIds'.");

        var ids = new List<string>();
        foreach (var id in idsEl.EnumerateArray())
            ids.Add(id.GetString()!);

        var clearExisting = !p.TryGetProperty("clearExisting", out var clearEl) || clearEl.GetBoolean();
        _state.SelectNodes(ids, clearExisting);
        return new AbilityResult(true, $"Selected {ids.Count} node(s).");
    }

    private AbilityResult SelectAllNodes()
    {
        _state.SelectAll();
        return new AbilityResult(true, $"Selected all {_state.Nodes.Count} node(s).");
    }

    private AbilityResult ClearSelection()
    {
        _state.ClearSelection();
        return new AbilityResult(true, "Selection cleared.");
    }

    private AbilityResult RemoveSelected()
    {
        var count = _state.SelectedNodeIds.Count;
        _state.RemoveSelectedNodes();
        return new AbilityResult(true, $"Removed {count} selected node(s).");
    }

    private AbilityResult SetSocketValue(JsonElement p)
    {
        if (!p.TryGetProperty("nodeId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'nodeId'.");
        if (!p.TryGetProperty("socketName", out var nameEl))
            return new AbilityResult(false, "Missing required parameter 'socketName'.");
        if (!p.TryGetProperty("value", out var valueEl))
            return new AbilityResult(false, "Missing required parameter 'value'.");

        var nodeId = idEl.GetString()!;
        var socketName = nameEl.GetString()!;
        var node = _state.Nodes.FirstOrDefault(n => n.Data.Id == nodeId);
        if (node is null)
            return new AbilityResult(false, $"Node '{nodeId}' not found.");

        var socket = node.Inputs.FirstOrDefault(s => s.Data.Name.Equals(socketName, StringComparison.OrdinalIgnoreCase));
        if (socket is null)
            return new AbilityResult(false, $"Input socket '{socketName}' not found on node '{node.Data.Name}'.",
                ErrorHint: $"Available inputs: {string.Join(", ", node.Inputs.Select(s => s.Data.Name))}");

        // Parse the value to the socket's expected type so SocketValue gets the right TypeName
        var rawValue = valueEl.ToString();
        object? typedValue = rawValue;
        var targetType = Type.GetType(socket.Data.TypeName);

        if (targetType is not null && targetType != typeof(string))
        {
            try
            {
                typedValue = targetType switch
                {
                    _ when targetType == typeof(int) || targetType == typeof(Int32) => int.Parse(rawValue),
                    _ when targetType == typeof(long) || targetType == typeof(Int64) => long.Parse(rawValue),
                    _ when targetType == typeof(double) || targetType == typeof(Double) => double.Parse(rawValue),
                    _ when targetType == typeof(float) || targetType == typeof(Single) => float.Parse(rawValue),
                    _ when targetType == typeof(decimal) || targetType == typeof(Decimal) => decimal.Parse(rawValue),
                    _ when targetType == typeof(bool) || targetType == typeof(Boolean) => bool.Parse(rawValue),
                    _ => Convert.ChangeType(rawValue, targetType)
                };
            }
            catch (Exception ex)
            {
                return new AbilityResult(false,
                    $"Cannot convert '{rawValue}' to {socket.Data.TypeName}: {ex.Message}",
                    ErrorHint: $"Socket '{socketName}' expects a value of type '{socket.Data.TypeName}'.");
            }
        }

        socket.SetValue(typedValue);
        return new AbilityResult(true, $"Set '{socketName}' on '{node.Data.Name}' to '{valueEl}'.");
    }
}
