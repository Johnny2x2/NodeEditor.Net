using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for managing connections between nodes.
/// </summary>
public sealed class ConnectionAbilityProvider : IAbilityProvider
{
    private readonly INodeEditorState _state;

    public ConnectionAbilityProvider(INodeEditorState state)
    {
        _state = state;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("connection.add", "Add Connection", "Connections",
            "Creates a connection between an output socket and an input socket on two nodes.",
            "Provide the output node ID, output socket name, input node ID, and input socket name. " +
            "Set isExecution to true for execution flow connections.",
            [
                new("outputNodeId", "string", "The ID of the source (output) node."),
                new("outputSocketName", "string", "The name of the output socket."),
                new("inputNodeId", "string", "The ID of the target (input) node."),
                new("inputSocketName", "string", "The name of the input socket."),
                new("isExecution", "boolean", "Whether this is an execution flow connection.", Required: false, DefaultValue: "false")
            ]),

        new("connection.remove", "Remove Connection", "Connections",
            "Removes a specific connection between two sockets.",
            "Provide the same parameters used when creating the connection.",
            [
                new("outputNodeId", "string", "The ID of the source node."),
                new("outputSocketName", "string", "The name of the output socket."),
                new("inputNodeId", "string", "The ID of the target node."),
                new("inputSocketName", "string", "The name of the input socket.")
            ]),

        new("connection.list", "List Connections", "Connections",
            "Lists all connections in the current graph.",
            "Returns all connections with source/target node and socket details.",
            [],
            ReturnDescription: "Array of connection objects."),

        new("connection.list_for_node", "List Connections for Node", "Connections",
            "Lists all connections going to or from a specific node.",
            "Provide the nodeId to filter connections.",
            [new("nodeId", "string", "The ID of the node.")],
            ReturnDescription: "Array of connection objects for the given node."),

        new("connection.remove_all_for_node", "Remove All Connections for Node", "Connections",
            "Removes all connections to and from a specific node.",
            "Provide the nodeId to remove all its connections.",
            [new("nodeId", "string", "The ID of the node.")])
    ];

    public Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(abilityId switch
        {
            "connection.add" => AddConnection(parameters),
            "connection.remove" => RemoveConnection(parameters),
            "connection.list" => ListConnections(),
            "connection.list_for_node" => ListForNode(parameters),
            "connection.remove_all_for_node" => RemoveAllForNode(parameters),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        });
    }

    private AbilityResult AddConnection(JsonElement p)
    {
        if (!p.TryGetProperty("outputNodeId", out var outNodeEl) ||
            !p.TryGetProperty("outputSocketName", out var outSocketEl) ||
            !p.TryGetProperty("inputNodeId", out var inNodeEl) ||
            !p.TryGetProperty("inputSocketName", out var inSocketEl))
            return new AbilityResult(false, "Missing required parameters.",
                ErrorHint: "Required: outputNodeId, outputSocketName, inputNodeId, inputSocketName");

        var outNodeId = outNodeEl.GetString()!;
        var inNodeId = inNodeEl.GetString()!;
        var outSocket = outSocketEl.GetString()!;
        var inSocket = inSocketEl.GetString()!;

        var outNode = _state.Nodes.FirstOrDefault(n => n.Data.Id == outNodeId);
        var inNode = _state.Nodes.FirstOrDefault(n => n.Data.Id == inNodeId);

        if (outNode is null)
            return new AbilityResult(false, $"Output node '{outNodeId}' not found.");
        if (inNode is null)
            return new AbilityResult(false, $"Input node '{inNodeId}' not found.");

        if (!outNode.Outputs.Any(s => s.Data.Name == outSocket))
            return new AbilityResult(false, $"Output socket '{outSocket}' not found on node '{outNode.Data.Name}'.",
                ErrorHint: $"Available outputs: {string.Join(", ", outNode.Outputs.Select(s => s.Data.Name))}");
        if (!inNode.Inputs.Any(s => s.Data.Name == inSocket))
            return new AbilityResult(false, $"Input socket '{inSocket}' not found on node '{inNode.Data.Name}'.",
                ErrorHint: $"Available inputs: {string.Join(", ", inNode.Inputs.Select(s => s.Data.Name))}");

        var isExec = p.TryGetProperty("isExecution", out var execEl) && execEl.GetBoolean();
        var connection = new ConnectionData(outNodeId, inNodeId, outSocket, inSocket, isExec);
        _state.AddConnection(connection);

        return new AbilityResult(true, $"Connection created: {outNode.Data.Name}.{outSocket} → {inNode.Data.Name}.{inSocket}");
    }

    private AbilityResult RemoveConnection(JsonElement p)
    {
        if (!p.TryGetProperty("outputNodeId", out var outNodeEl) ||
            !p.TryGetProperty("outputSocketName", out var outSocketEl) ||
            !p.TryGetProperty("inputNodeId", out var inNodeEl) ||
            !p.TryGetProperty("inputSocketName", out var inSocketEl))
            return new AbilityResult(false, "Missing required parameters.");

        var match = _state.Connections.FirstOrDefault(c =>
            c.OutputNodeId == outNodeEl.GetString() &&
            c.InputNodeId == inNodeEl.GetString() &&
            c.OutputSocketName == outSocketEl.GetString() &&
            c.InputSocketName == inSocketEl.GetString());

        if (match is null)
            return new AbilityResult(false, "Connection not found.");

        _state.RemoveConnection(match);
        return new AbilityResult(true, "Connection removed.");
    }

    private AbilityResult ListConnections()
    {
        var connections = _state.Connections.Select(c => new
        {
            c.OutputNodeId,
            OutputNodeName = _state.Nodes.FirstOrDefault(n => n.Data.Id == c.OutputNodeId)?.Data.Name,
            c.OutputSocketName,
            c.InputNodeId,
            InputNodeName = _state.Nodes.FirstOrDefault(n => n.Data.Id == c.InputNodeId)?.Data.Name,
            c.InputSocketName,
            c.IsExecution
        }).ToList();

        return new AbilityResult(true, $"Found {connections.Count} connection(s).", Data: connections);
    }

    private AbilityResult ListForNode(JsonElement p)
    {
        if (!p.TryGetProperty("nodeId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'nodeId'.");

        var nodeId = idEl.GetString()!;
        var connections = _state.Connections
            .Where(c => c.OutputNodeId == nodeId || c.InputNodeId == nodeId)
            .Select(c => new
            {
                c.OutputNodeId,
                OutputNodeName = _state.Nodes.FirstOrDefault(n => n.Data.Id == c.OutputNodeId)?.Data.Name,
                c.OutputSocketName,
                c.InputNodeId,
                InputNodeName = _state.Nodes.FirstOrDefault(n => n.Data.Id == c.InputNodeId)?.Data.Name,
                c.InputSocketName,
                c.IsExecution
            }).ToList();

        return new AbilityResult(true, $"Found {connections.Count} connection(s) for node '{nodeId}'.", Data: connections);
    }

    private AbilityResult RemoveAllForNode(JsonElement p)
    {
        if (!p.TryGetProperty("nodeId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'nodeId'.");

        var nodeId = idEl.GetString()!;
        _state.RemoveConnectionsToNode(nodeId);
        return new AbilityResult(true, $"Removed all connections for node '{nodeId}'.");
    }
}
