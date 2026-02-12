using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Execution;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Blazor.Tests;

/// <summary>
/// Fluent builder for constructing test graphs (NodeData + ConnectionData collections).
/// </summary>
public sealed class TestGraphBuilder
{
    private readonly List<NodeData> _nodes = new();
    private readonly List<ConnectionData> _connections = new();

    public TestGraphBuilder AddNodeFromDefinition(
        NodeRegistryService registry,
        string definitionName,
        string nodeId,
        params (string socketName, object? value)[] inputOverrides)
    {
        registry.EnsureInitialized();
        var def = registry.Definitions.First(d => d.Name == definitionName);

        var node = def.Factory() with { Id = nodeId };
        if (inputOverrides.Length > 0)
        {
            var updatedInputs = node.Inputs.Select(s =>
            {
                var match = inputOverrides.FirstOrDefault(o => o.socketName == s.Name);
                if (!string.IsNullOrEmpty(match.socketName))
                    return s with { Value = SocketValue.FromObject(match.value) };
                return s;
            }).ToArray();

            node = node with { Inputs = updatedInputs };
        }

        _nodes.Add(node);
        return this;
    }

    /// <summary>
    /// Adds a <see cref="NodeBase"/> subclass to the graph.
    /// Uses <see cref="NodeDiscoveryService.BuildDefinitionFromType"/> to create a proper definition,
    /// then calls Factory() and assigns the generated ID.
    /// </summary>
    public TestGraphBuilder AddNode(NodeBase node, out string nodeId)
    {
        var discovery = new NodeDiscoveryService();
        var definition = discovery.BuildDefinitionFromType(node.GetType())!;
        var nodeData = definition.Factory();
        nodeId = nodeData.Id;
        _nodes.Add(nodeData);
        return this;
    }

    /// <summary>
    /// Adds a <see cref="NodeBase"/> subclass with a specific ID.
    /// </summary>
    public TestGraphBuilder AddNode(NodeBase node, string nodeId)
    {
        var discovery = new NodeDiscoveryService();
        var definition = discovery.BuildDefinitionFromType(node.GetType())!;
        var nodeData = definition.Factory() with { Id = nodeId };
        _nodes.Add(nodeData);
        return this;
    }

    /// <summary>
    /// Adds a pre-built <see cref="NodeData"/> directly.
    /// </summary>
    public TestGraphBuilder AddNodeData(NodeData nodeData)
    {
        _nodes.Add(nodeData);
        return this;
    }

    /// <summary>
    /// Connects an execution output to an execution input.
    /// </summary>
    public TestGraphBuilder ConnectExecution(string fromNodeId, string fromSocket, string toNodeId, string toSocket)
    {
        _connections.Add(new ConnectionData(fromNodeId, toNodeId, fromSocket, toSocket, true));
        return this;
    }

    /// <summary>
    /// Connects a data output to a data input.
    /// </summary>
    public TestGraphBuilder ConnectData(string fromNodeId, string fromSocket, string toNodeId, string toSocket)
    {
        _connections.Add(new ConnectionData(fromNodeId, toNodeId, fromSocket, toSocket, false));
        return this;
    }

    /// <summary>
    /// Builds the graph, returning the node and connection collections.
    /// </summary>
    public (IReadOnlyList<NodeData> Nodes, IReadOnlyList<ConnectionData> Connections) Build()
        => (_nodes.AsReadOnly(), _connections.AsReadOnly());
}
