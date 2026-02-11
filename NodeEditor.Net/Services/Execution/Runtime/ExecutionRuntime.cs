using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

internal sealed class ExecutionRuntime
{
    private readonly IReadOnlyList<NodeData> _nodes;
    private readonly Dictionary<string, NodeData> _nodeMap;
    private readonly Dictionary<(string nodeId, string socketName), List<(string targetNodeId, string targetSocket)>> _execConnections;
    private readonly Dictionary<(string nodeId, string socketName), (string sourceNodeId, string sourceSocket)> _dataInputConnections;
    private readonly Dictionary<string, NodeBase?> _nodeInstances;
    private readonly Dictionary<string, NodeDefinition> _nodeDefinitions;
    private readonly HashSet<string> _createdNodes = new(StringComparer.Ordinal);

    public INodeRuntimeStorage RuntimeStorage { get; }
    public IServiceProvider Services { get; }
    public CancellationToken CancellationToken { get; }
    public ExecutionGate Gate { get; }

    /// <summary>
    /// All node instances created during this execution, for cleanup.
    /// </summary>
    internal IReadOnlyDictionary<string, NodeBase?> NodeInstances => _nodeInstances;

    public event EventHandler<NodeExecutionEventArgs>? NodeStarted;
    public event EventHandler<NodeExecutionEventArgs>? NodeCompleted;
    public event EventHandler<NodeExecutionFailedEventArgs>? NodeFailed;
    public event EventHandler<FeedbackMessageEventArgs>? FeedbackReceived;

    internal ExecutionRuntime(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeRuntimeStorage runtimeStorage,
        IServiceProvider services,
        INodeRegistryService registry,
        ExecutionGate gate,
        CancellationToken ct)
    {
        _nodes = nodes;
        RuntimeStorage = runtimeStorage;
        Services = services;
        CancellationToken = ct;
        Gate = gate;
        _nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        _execConnections = BuildExecConnectionMap(connections);
        _dataInputConnections = BuildDataInputConnectionMap(connections);
        _nodeDefinitions = ResolveDefinitions(nodes, registry);
        _nodeInstances = new Dictionary<string, NodeBase?>(StringComparer.Ordinal);
    }

    internal NodeBase? GetOrCreateInstance(string nodeId)
    {
        if (_nodeInstances.TryGetValue(nodeId, out var existing))
        {
            return existing;
        }

        if (!_nodeDefinitions.TryGetValue(nodeId, out var definition) || definition.NodeType is null)
        {
            _nodeInstances[nodeId] = null;
            return null;
        }

        var instance = (NodeBase)Activator.CreateInstance(definition.NodeType)!;
        instance.NodeId = nodeId;
        _nodeInstances[nodeId] = instance;
        return instance;
    }

    internal async Task ExecuteNodeByIdAsync(string nodeId)
    {
        if (!_nodeMap.TryGetValue(nodeId, out var node))
        {
            return;
        }

        if (!node.Callable && RuntimeStorage.IsNodeExecuted(nodeId))
        {
            return;
        }

        NodeStarted?.Invoke(this, new NodeExecutionEventArgs(node));

        try
        {
            await ResolveAllDataInputsAsync(node).ConfigureAwait(false);

            if (!_nodeDefinitions.TryGetValue(nodeId, out var definition))
            {
                throw new InvalidOperationException($"No node definition found for node '{node.Name}'.");
            }

            var context = new NodeExecutionContextImpl(node, this);

            if (definition.InlineExecutor is not null)
            {
                await definition.InlineExecutor(context, CancellationToken).ConfigureAwait(false);
            }
            else
            {
                var instance = GetOrCreateInstance(nodeId);
                if (instance is null)
                {
                    throw new InvalidOperationException($"No node implementation available for '{node.Name}'.");
                }

                if (_createdNodes.Add(nodeId))
                {
                    await instance.OnCreatedAsync(Services).ConfigureAwait(false);
                }

                await instance.ExecuteAsync(context, CancellationToken).ConfigureAwait(false);
            }

            RuntimeStorage.MarkNodeExecuted(nodeId);
            NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
        }
        catch (Exception ex)
        {
            NodeFailed?.Invoke(this, new NodeExecutionFailedEventArgs(node, ex));
            throw;
        }
    }

    internal async Task ResolveAllDataInputsAsync(NodeData node)
    {
        foreach (var input in node.Inputs.Where(i => !i.IsExecution))
        {
            if (RuntimeStorage.TryGetSocketValue(node.Id, input.Name, out _))
            {
                continue;
            }

            var resolved = await ResolveInputAsync(node, input.Name).ConfigureAwait(false);
            if (resolved is not null)
            {
                RuntimeStorage.SetSocketValue(node.Id, input.Name, resolved);
                continue;
            }

            if (input.Value is not null)
            {
                var defaultValue = DeserializeSocketValue<object?>(input.Value);
                RuntimeStorage.SetSocketValue(node.Id, input.Name, defaultValue);
            }
        }
    }

    internal async Task<object?> ResolveInputAsync(NodeData node, string socketName)
    {
        if (!_dataInputConnections.TryGetValue((node.Id, socketName), out var source))
        {
            return null;
        }

        if (!_nodeMap.TryGetValue(source.sourceNodeId, out var sourceNode))
        {
            return null;
        }

        if (!sourceNode.Callable && !RuntimeStorage.IsNodeExecuted(sourceNode.Id))
        {
            await ExecuteNodeByIdAsync(sourceNode.Id).ConfigureAwait(false);
        }

        return RuntimeStorage.GetSocketValue(source.sourceNodeId, source.sourceSocket);
    }

    internal List<(string, string)> GetExecutionTargets(string nodeId, string socketName)
    {
        if (_execConnections.TryGetValue((nodeId, socketName), out var targets))
        {
            return targets;
        }

        return new List<(string, string)>();
    }

    internal StreamSocketInfo? GetStreamInfo(NodeData node, string itemSocketName)
    {
        if (!_nodeDefinitions.TryGetValue(node.Id, out var definition))
        {
            return null;
        }

        return definition.StreamSockets?.FirstOrDefault(s => s.ItemDataSocket == itemSocketName);
    }

    internal StreamMode GetStreamMode(NodeData node) => StreamMode.Sequential;

    internal T DeserializeSocketValue<T>(SocketValue socketValue)
    {
        if (typeof(T) == typeof(object) && socketValue.TypeName is not null && socketValue.Json is not null)
        {
            var type = Type.GetType(socketValue.TypeName);
            if (type is not null)
            {
                return (T)System.Text.Json.JsonSerializer.Deserialize(socketValue.Json.Value.GetRawText(), type)!;
            }
        }

        return socketValue.ToObject<T>()!;
    }

    internal void RaiseFeedback(string message, NodeData node, ExecutionFeedbackType type, object? tag)
    {
        FeedbackReceived?.Invoke(this, new FeedbackMessageEventArgs(message, node, type, tag));
    }

    private static Dictionary<(string nodeId, string socketName), List<(string targetNodeId, string targetSocket)>> BuildExecConnectionMap(
        IReadOnlyList<ConnectionData> connections)
    {
        var map = new Dictionary<(string, string), List<(string, string)>>();

        foreach (var connection in connections.Where(c => c.IsExecution))
        {
            var key = (connection.OutputNodeId, connection.OutputSocketName);
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<(string, string)>();
                map[key] = list;
            }

            list.Add((connection.InputNodeId, connection.InputSocketName));
        }

        return map;
    }

    private static Dictionary<(string nodeId, string socketName), (string sourceNodeId, string sourceSocket)> BuildDataInputConnectionMap(
        IReadOnlyList<ConnectionData> connections)
    {
        var map = new Dictionary<(string, string), (string, string)>();

        foreach (var connection in connections.Where(c => !c.IsExecution))
        {
            var key = (connection.InputNodeId, connection.InputSocketName);
            if (!map.ContainsKey(key))
            {
                map[key] = (connection.OutputNodeId, connection.OutputSocketName);
            }
        }

        return map;
    }

    private static Dictionary<string, NodeDefinition> ResolveDefinitions(
        IReadOnlyList<NodeData> nodes,
        INodeRegistryService registry)
    {
        registry.EnsureInitialized();
        var definitions = registry.Definitions;
        var byId = definitions.ToDictionary(d => d.Id, StringComparer.Ordinal);
        var byName = definitions
            .GroupBy(d => d.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        var map = new Dictionary<string, NodeDefinition>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (!string.IsNullOrWhiteSpace(node.DefinitionId) && byId.TryGetValue(node.DefinitionId!, out var definition))
            {
                map[node.Id] = definition;
                continue;
            }

            if (byName.TryGetValue(node.Name, out var nameDefinition))
            {
                map[node.Id] = nameDefinition;
            }
        }

        return map;
    }
}
