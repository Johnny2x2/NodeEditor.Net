using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
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
    private readonly NodeExecutionOptions _options;
    private readonly Dictionary<(string nodeId, string completedExecSocket), List<Task>> _pendingStreamItemTasks = new();
    private readonly Func<string, IServiceProvider?>? _resolveServicesForDefinition;
    private readonly Dictionary<string, IServiceProvider> _nodeServices = new(StringComparer.Ordinal);
    private int _callDepth;

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
        NodeExecutionOptions options,
        CancellationToken ct,
        Func<string, IServiceProvider?>? resolveServicesForDefinition = null)
    {
        _nodes = nodes;
        RuntimeStorage = runtimeStorage;
        Services = services;
        CancellationToken = ct;
        Gate = gate;
        _options = options;
        _resolveServicesForDefinition = resolveServicesForDefinition;
        _nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        _execConnections = BuildExecConnectionMap(connections);
        _dataInputConnections = BuildDataInputConnectionMap(connections);
        _nodeDefinitions = ResolveDefinitions(nodes, registry);
        _nodeInstances = new Dictionary<string, NodeBase?>(StringComparer.Ordinal);
        BuildNodeServiceMap();
    }

    internal IServiceProvider GetServicesForNode(string nodeId)
    {
        return _nodeServices.TryGetValue(nodeId, out var services)
            ? services
            : Services;
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

        // Guard against execution-flow cycles that would cause a stack overflow.
        if (++_callDepth > _options.MaxCallDepth)
        {
            _callDepth--;
            throw new InvalidOperationException(
                $"Execution call depth exceeded {_options.MaxCallDepth}. " +
                $"This usually indicates an execution-flow cycle involving node '{node.Name}' ({nodeId}). " +
                $"Check for circular execution connections in your graph.");
        }

        try
        {
            await ExecuteNodeCoreAsync(nodeId, node).ConfigureAwait(false);
        }
        finally
        {
            _callDepth--;
        }
    }

    private async Task ExecuteNodeCoreAsync(string nodeId, NodeData node)
    {
        NodeStarted?.Invoke(this, new NodeExecutionEventArgs(node));

        try
        {
            await ResolveAllDataInputsAsync(node).ConfigureAwait(false);

            // Variable nodes are special: they have no NodeBase implementation and are executed
            // by reading/writing runtime storage variables.
            if (VariableNodeExecutor.IsVariableNode(node))
            {
                VariableNodeExecutor.Execute(node, RuntimeStorage);

                if (VariableNodeExecutor.IsSetNode(node))
                {
                    await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
                    var targets = GetExecutionTargets(node.Id, "Exit");
                    foreach (var (targetNodeId, _) in targets)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        await ExecuteNodeByIdAsync(targetNodeId).ConfigureAwait(false);
                    }
                }

                NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
                return;
            }

            // Event nodes are special: they have no NodeBase implementation.
            // Trigger nodes fire the event bus; listener nodes are handled by the bus
            // and should be skipped when reached as initiators.
            if (EventNodeExecutor.IsEventNode(node))
            {
                if (EventNodeExecutor.IsTriggerNode(node))
                {
                    await EventNodeExecutor.ExecuteTriggerAsync(node, RuntimeStorage, CancellationToken)
                        .ConfigureAwait(false);

                    await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
                    var targets = GetExecutionTargets(node.Id, "Exit");
                    foreach (var (targetNodeId, _) in targets)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        await ExecuteNodeByIdAsync(targetNodeId).ConfigureAwait(false);
                    }
                }

                // Listener nodes: execution is driven by the event bus handler
                // registered in NodeExecutionService.RegisterEventListeners.
                // When reached as an initiator, we simply mark them done.
                RuntimeStorage.MarkNodeExecuted(nodeId);
                NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
                return;
            }

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
                    await instance.OnCreatedAsync(GetServicesForNode(nodeId)).ConfigureAwait(false);
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
            if (!node.Callable && RuntimeStorage.TryGetSocketValue(node.Id, input.Name, out _))
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

    /// <summary>
    /// Executes a node using a scoped storage layer. The scoped storage provides
    /// write-isolation (outputs and execution tracking go to the scope) while
    /// reading through to the parent for upstream values resolved earlier.
    /// Used by parallel loop iterations.
    /// </summary>
    internal async Task ExecuteNodeByIdScopedAsync(string nodeId, INodeRuntimeStorage scope)
    {
        if (!_nodeMap.TryGetValue(nodeId, out var node))
        {
            return;
        }

        // In scoped execution, callable nodes always re-execute (scope has fresh execution tracking).
        // Non-callable upstream nodes that were already executed in the parent are resolved via
        // read-through in the layered storage.
        if (!node.Callable && scope.IsNodeExecuted(nodeId))
        {
            return;
        }

        NodeStarted?.Invoke(this, new NodeExecutionEventArgs(node));

        try
        {
            await ResolveAllDataInputsScopedAsync(node, scope).ConfigureAwait(false);

            if (VariableNodeExecutor.IsVariableNode(node))
            {
                VariableNodeExecutor.Execute(node, scope);

                if (VariableNodeExecutor.IsSetNode(node))
                {
                    await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
                    var targets = GetExecutionTargets(node.Id, "Exit");
                    foreach (var (targetNodeId, _) in targets)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        await ExecuteNodeByIdScopedAsync(targetNodeId, scope).ConfigureAwait(false);
                    }
                }

                NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
                return;
            }

            // Event nodes in scoped execution: same handling as unscoped.
            if (EventNodeExecutor.IsEventNode(node))
            {
                if (EventNodeExecutor.IsTriggerNode(node))
                {
                    await EventNodeExecutor.ExecuteTriggerAsync(node, scope, CancellationToken)
                        .ConfigureAwait(false);

                    await Gate.WaitAsync(CancellationToken).ConfigureAwait(false);
                    var targets = GetExecutionTargets(node.Id, "Exit");
                    foreach (var (targetNodeId, _) in targets)
                    {
                        CancellationToken.ThrowIfCancellationRequested();
                        await ExecuteNodeByIdScopedAsync(targetNodeId, scope).ConfigureAwait(false);
                    }
                }

                scope.MarkNodeExecuted(nodeId);
                NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
                return;
            }

            if (!_nodeDefinitions.TryGetValue(nodeId, out var definition))
            {
                throw new InvalidOperationException($"No node definition found for node '{node.Name}'.");
            }

            var context = new ScopedNodeExecutionContext(node, this, scope);

            if (definition.InlineExecutor is not null)
            {
                await definition.InlineExecutor(context, CancellationToken).ConfigureAwait(false);
            }
            else
            {
                // For scoped execution, create a fresh instance per scope to avoid shared mutable state.
                NodeBase? instance;
                if (!_nodeDefinitions.TryGetValue(nodeId, out var def) || def.NodeType is null)
                {
                    throw new InvalidOperationException($"No node implementation available for '{node.Name}'.");
                }

                instance = (NodeBase)Activator.CreateInstance(def.NodeType)!;
                instance.NodeId = nodeId;

                await instance.OnCreatedAsync(GetServicesForNode(nodeId)).ConfigureAwait(false);
                await instance.ExecuteAsync(context, CancellationToken).ConfigureAwait(false);
            }

            scope.MarkNodeExecuted(nodeId);
            NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
        }
        catch (Exception ex)
        {
            NodeFailed?.Invoke(this, new NodeExecutionFailedEventArgs(node, ex));
            throw;
        }
    }

    internal async Task ResolveAllDataInputsScopedAsync(NodeData node, INodeRuntimeStorage scope)
    {
        foreach (var input in node.Inputs.Where(i => !i.IsExecution))
        {
            if (!node.Callable && scope.TryGetSocketValue(node.Id, input.Name, out _))
            {
                continue;
            }

            var resolved = await ResolveInputScopedAsync(node, input.Name, scope).ConfigureAwait(false);
            if (resolved is not null)
            {
                scope.SetSocketValue(node.Id, input.Name, resolved);
                continue;
            }

            if (input.Value is not null)
            {
                var defaultValue = DeserializeSocketValue<object?>(input.Value);
                scope.SetSocketValue(node.Id, input.Name, defaultValue);
            }
        }
    }

    internal async Task<object?> ResolveInputScopedAsync(NodeData node, string socketName, INodeRuntimeStorage scope)
    {
        if (!_dataInputConnections.TryGetValue((node.Id, socketName), out var source))
        {
            return null;
        }

        if (!_nodeMap.TryGetValue(source.sourceNodeId, out var sourceNode))
        {
            return null;
        }

        // For non-callable upstream nodes not yet executed: first check if the parent
        // storage already has the value (read-through). If not, execute via the shared
        // runtime (upstream data nodes produce the same result for all iterations).
        if (!sourceNode.Callable && !scope.IsNodeExecuted(sourceNode.Id))
        {
            // Check if a value already exists via read-through (i.e., resolved before the loop)
            if (!scope.TryGetSocketValue(source.sourceNodeId, source.sourceSocket, out _))
            {
                await ExecuteNodeByIdAsync(sourceNode.Id).ConfigureAwait(false);
            }
        }

        return scope.GetSocketValue(source.sourceNodeId, source.sourceSocket);
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

    internal StreamMode GetStreamMode(NodeData node) => _options.StreamMode;

    internal void RegisterStreamItemTask(string nodeId, string? completedExecSocketName, Task task)
    {
        if (string.IsNullOrWhiteSpace(completedExecSocketName))
        {
            ObserveTaskFailure(task);
            return;
        }

        lock (_pendingStreamItemTasks)
        {
            var key = (nodeId, completedExecSocketName);
            if (!_pendingStreamItemTasks.TryGetValue(key, out var list))
            {
                list = new List<Task>();
                _pendingStreamItemTasks[key] = list;
            }

            list.Add(task);
        }

        ObserveTaskFailure(task);
    }

    internal async Task WaitForStreamItemsAsync(string nodeId, string completedExecSocketName)
    {
        List<Task>? snapshot;
        lock (_pendingStreamItemTasks)
        {
            var key = (nodeId, completedExecSocketName);
            if (!_pendingStreamItemTasks.TryGetValue(key, out snapshot) || snapshot.Count == 0)
            {
                return;
            }

            _pendingStreamItemTasks.Remove(key);
        }

        try
        {
            await Task.WhenAll(snapshot).ConfigureAwait(false);
        }
        catch
        {
            // Fire-and-forget mode shouldn't crash the producer due to downstream item failures.
            // Faults are observed via ObserveTaskFailure().
        }
    }

    private static void ObserveTaskFailure(Task task)
    {
        _ = task.ContinueWith(
            t => _ = t.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

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

    private void BuildNodeServiceMap()
    {
        if (_resolveServicesForDefinition is null)
        {
            return;
        }

        foreach (var node in _nodes)
        {
            if (!_nodeDefinitions.TryGetValue(node.Id, out var definition))
            {
                continue;
            }

            var pluginServices = _resolveServicesForDefinition(definition.Id);
            if (pluginServices is not null)
            {
                _nodeServices[node.Id] = pluginServices;
            }
        }
    }
}
