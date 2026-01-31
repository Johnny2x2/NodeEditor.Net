using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class NodeExecutionService
{
    private readonly ExecutionPlanner _planner;
    private readonly SocketTypeResolver _typeResolver;

    public NodeExecutionService(ExecutionPlanner planner, SocketTypeResolver typeResolver)
    {
        _planner = planner;
        _typeResolver = typeResolver;
    }

    public event EventHandler<NodeExecutionEventArgs>? NodeStarted;
    public event EventHandler<NodeExecutionEventArgs>? NodeCompleted;
    public event EventHandler<Exception>? ExecutionFailed;
    public event EventHandler? ExecutionCanceled;
    public event EventHandler<ExecutionLayerEventArgs>? LayerStarted;
    public event EventHandler<ExecutionLayerEventArgs>? LayerCompleted;

    public async Task ExecuteAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        object nodeContext,
        NodeExecutionOptions? options,
        CancellationToken token)
    {
        var effectiveOptions = options ?? NodeExecutionOptions.Default;

        if (effectiveOptions.Mode == ExecutionMode.Sequential)
        {
            await ExecuteSequentialAsync(nodes, connections, context, nodeContext, token).ConfigureAwait(false);
            return;
        }

        var plan = _planner.BuildPlan(nodes, connections);
        await ExecutePlannedAsync(plan, connections, context, nodeContext, effectiveOptions, token).ConfigureAwait(false);
    }

    public async Task ExecutePlannedAsync(
        ExecutionPlan plan,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        object nodeContext,
        NodeExecutionOptions options,
        CancellationToken token)
    {
        var invoker = new NodeMethodInvoker(nodeContext, _typeResolver);
        var nodeMap = plan.Layers.SelectMany(layer => layer.Nodes).ToDictionary(n => n.Id, StringComparer.Ordinal);
        var feedbackContext = nodeContext as INodeMethodContext;
        var breakExecution = false;

        void FeedbackHandler(string message, NodeData node, ExecutionFeedbackType type, object? tag, bool breakFlag)
        {
            if (breakFlag)
            {
                breakExecution = true;
            }
        }

        if (feedbackContext is not null)
        {
            feedbackContext.FeedbackInfo += FeedbackHandler;
        }

        try
        {
            foreach (var layer in plan.Layers)
            {
                token.ThrowIfCancellationRequested();
                if (breakExecution)
                {
                    break;
                }

                LayerStarted?.Invoke(this, new ExecutionLayerEventArgs(layer));

                if (options.Mode == ExecutionMode.Sequential)
                {
                    foreach (var node in layer.Nodes)
                    {
                        token.ThrowIfCancellationRequested();
                        if (breakExecution)
                        {
                            break;
                        }

                        await ExecuteNodeAsync(node, connections, nodeMap, context, invoker, feedbackContext, token).ConfigureAwait(false);
                    }
                }
                else
                {
                    using var throttler = new SemaphoreSlim(Math.Max(1, options.MaxDegreeOfParallelism));
                    var tasks = layer.Nodes.Select(async node =>
                    {
                        await throttler.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            await ExecuteNodeAsync(node, connections, nodeMap, context, invoker, feedbackContext, token).ConfigureAwait(false);
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    });

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }

                LayerCompleted?.Invoke(this, new ExecutionLayerEventArgs(layer));
            }
        }
        catch (OperationCanceledException)
        {
            ExecutionCanceled?.Invoke(this, EventArgs.Empty);
            throw;
        }
        catch (Exception ex)
        {
            ExecutionFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            if (feedbackContext is not null)
            {
                feedbackContext.FeedbackInfo -= FeedbackHandler;
            }
        }
    }

    public async Task ExecuteGroupAsync(
        GroupNodeData group,
        INodeExecutionContext parentContext,
        object nodeContext,
        NodeExecutionOptions? options,
        CancellationToken token)
    {
        var childContext = parentContext.CreateChild(group.Id);

        foreach (var mapping in group.InputMappings)
        {
            var value = parentContext.GetSocketValue(group.Id, mapping.GroupSocketName);
            childContext.SetSocketValue(mapping.NodeId, mapping.SocketName, value);
        }

        var plan = _planner.BuildPlan(group.Nodes, group.Connections);
        await ExecutePlannedAsync(plan, group.Connections, childContext, nodeContext, options ?? NodeExecutionOptions.Default, token)
            .ConfigureAwait(false);

        foreach (var mapping in group.OutputMappings)
        {
            var value = childContext.GetSocketValue(mapping.NodeId, mapping.SocketName);
            parentContext.SetSocketValue(group.Id, mapping.GroupSocketName, value);
        }
    }

    private async Task ExecuteSequentialAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        object nodeContext,
        CancellationToken token)
    {
        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var invoker = new NodeMethodInvoker(nodeContext, _typeResolver);
        var feedbackContext = nodeContext as INodeMethodContext;
        var breakExecution = false;

        void FeedbackHandler(string message, NodeData node, ExecutionFeedbackType type, object? tag, bool breakFlag)
        {
            if (breakFlag)
            {
                breakExecution = true;
            }
        }

        if (feedbackContext is not null)
        {
            feedbackContext.FeedbackInfo += FeedbackHandler;
        }

        try
        {
            var entryNodes = nodes.Where(n => n.ExecInit).ToList();
            if (entryNodes.Count == 0)
            {
                entryNodes = nodes.Where(n => n.Callable).ToList();
            }

            var queue = new Queue<NodeData>(entryNodes);

            while (queue.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                if (breakExecution)
                {
                    break;
                }

                var node = queue.Dequeue();
                await ExecuteNodeAsync(node, connections, nodeMap, context, invoker, feedbackContext, token).ConfigureAwait(false);

                var next = SelectNextExecutionNode(node, connections, nodeMap, context);
                if (next is not null)
                {
                    queue.Enqueue(next);
                }
            }
        }
        catch (OperationCanceledException)
        {
            ExecutionCanceled?.Invoke(this, EventArgs.Empty);
            throw;
        }
        catch (Exception ex)
        {
            ExecutionFailed?.Invoke(this, ex);
            throw;
        }
        finally
        {
            if (feedbackContext is not null)
            {
                feedbackContext.FeedbackInfo -= FeedbackHandler;
            }
        }
    }

    private async Task ExecuteNodeAsync(
        NodeData node,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        CancellationToken token)
    {
        NodeStarted?.Invoke(this, new NodeExecutionEventArgs(node));

        if (feedbackContext is not null)
        {
            feedbackContext.CurrentProcessingNode = node;
        }

        await ResolveInputsAsync(node, connections, nodeMap, context, invoker, feedbackContext, token).ConfigureAwait(false);

        var method = invoker.Resolve(node);
        if (method is null)
        {
            throw new InvalidOperationException($"No method binding found for node '{node.Name}'.");
        }

        await invoker.InvokeAsync(node, method, context, token).ConfigureAwait(false);

        NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
    }

    private async Task ResolveInputsAsync(
        NodeData node,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        CancellationToken token)
    {
        var connectionsByInput = connections
            .Where(c => !c.IsExecution)
            .GroupBy(c => (c.InputNodeId, c.InputSocketName))
            .ToDictionary(g => g.Key, g => g.ToList());

        var stack = new HashSet<string>(StringComparer.Ordinal);

        async Task ResolveNodeAsync(NodeData target)
        {
            if (!stack.Add(target.Id))
            {
                return;
            }

            foreach (var input in target.Inputs.Where(i => !i.IsExecution))
            {
                token.ThrowIfCancellationRequested();

                if (!connectionsByInput.TryGetValue((target.Id, input.Name), out var inbound))
                {
                    continue;
                }

                foreach (var connection in inbound)
                {
                    if (!nodeMap.TryGetValue(connection.OutputNodeId, out var sourceNode))
                    {
                        continue;
                    }

                    await ResolveNodeAsync(sourceNode).ConfigureAwait(false);

                    if (!sourceNode.Callable && !context.IsNodeExecuted(sourceNode.Id))
                    {
                        await ExecuteNodeAsync(sourceNode, connections, nodeMap, context, invoker, feedbackContext, token)
                            .ConfigureAwait(false);
                    }

                    var outputValue = context.GetSocketValue(sourceNode.Id, connection.OutputSocketName);
                    context.SetSocketValue(target.Id, input.Name, outputValue);
                }
            }

            stack.Remove(target.Id);
        }

        await ResolveNodeAsync(node).ConfigureAwait(false);
    }

    private static NodeData? SelectNextExecutionNode(
        NodeData node,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context)
    {
        var outgoing = connections
            .Where(c => c.IsExecution && c.OutputNodeId == node.Id)
            .ToList();

        var signaled = outgoing.FirstOrDefault(connection =>
        {
            var value = context.GetSocketValue(node.Id, connection.OutputSocketName) as ExecutionPath;
            return value?.IsSignaled == true;
        });

        if (signaled is not null && nodeMap.TryGetValue(signaled.InputNodeId, out var signaledNode))
        {
            return signaledNode;
        }

        var main = outgoing.FirstOrDefault(connection =>
            connection.OutputSocketName.Equals("Exit", StringComparison.OrdinalIgnoreCase));

        if (main is not null && nodeMap.TryGetValue(main.InputNodeId, out var mainNode))
        {
            return mainNode;
        }

        var fallback = outgoing.FirstOrDefault();
        if (fallback is not null && nodeMap.TryGetValue(fallback.InputNodeId, out var fallbackNode))
        {
            return fallbackNode;
        }

        return null;
    }
}
