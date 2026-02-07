using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class NodeExecutionService : INodeExecutionService
{
    private readonly ExecutionPlanner _planner;
    private readonly ISocketTypeResolver _typeResolver;
    private const int MaxLoopIterations = 10_000;

    public NodeExecutionService(ExecutionPlanner planner, ISocketTypeResolver typeResolver)
    {
        _planner = planner;
        _typeResolver = typeResolver;
    }

    public event EventHandler<NodeExecutionEventArgs>? NodeStarted;
    public event EventHandler<NodeExecutionEventArgs>? NodeCompleted;
    public event EventHandler<NodeExecutionFailedEventArgs>? NodeFailed;
    public event EventHandler<Exception>? ExecutionFailed;
    public event EventHandler? ExecutionCanceled;
    public event EventHandler<ExecutionLayerEventArgs>? LayerStarted;
    public event EventHandler<ExecutionLayerEventArgs>? LayerCompleted;
    public event EventHandler<FeedbackMessageEventArgs>? FeedbackReceived;

    public ExecutionGate Gate { get; } = new();

    // ────────────────────────── Public entry points ──────────────────────────

    public async Task ExecuteAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeExecutionContext context,
        object nodeContext,
        NodeExecutionOptions? options,
        CancellationToken token)
    {
        var effectiveOptions = options ?? NodeExecutionOptions.Default;
        var plan = _planner.BuildHierarchicalPlan(nodes, connections);

        var nodeMap = nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var invoker = new NodeMethodInvoker(nodeContext, _typeResolver);
        var feedbackContext = nodeContext as INodeMethodContext;
        var breakExecution = false;

        // Register Custom Event (listener) node handlers on the event bus.
        // When a Trigger Event node fires, the listener's downstream path executes.
        RegisterEventListeners(nodes, connections, nodeMap, context, invoker, feedbackContext, effectiveOptions, token);

        void FeedbackHandler(string message, NodeData node, ExecutionFeedbackType type, object? tag, bool breakFlag)
        {
            if (breakFlag) breakExecution = true;
            FeedbackReceived?.Invoke(this, new FeedbackMessageEventArgs(message, node, type, tag));
        }

        if (feedbackContext is not null)
            feedbackContext.FeedbackInfo += FeedbackHandler;

        try
        {
            await ExecuteStepsAsync(
                plan.Steps, connections, nodeMap, context, invoker, feedbackContext,
                effectiveOptions, () => breakExecution, token).ConfigureAwait(false);
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
                feedbackContext.FeedbackInfo -= FeedbackHandler;
        }
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
        var nodeMap = plan.Layers.SelectMany(l => l.Nodes).ToDictionary(n => n.Id, StringComparer.Ordinal);
        var feedbackContext = nodeContext as INodeMethodContext;
        var breakExecution = false;

        void FeedbackHandler(string message, NodeData node, ExecutionFeedbackType type, object? tag, bool breakFlag)
        {
            if (breakFlag) breakExecution = true;
            FeedbackReceived?.Invoke(this, new FeedbackMessageEventArgs(message, node, type, tag));
        }

        if (feedbackContext is not null)
            feedbackContext.FeedbackInfo += FeedbackHandler;

        try
        {
            // Convert legacy plan to steps
            var steps = plan.Layers.Select(l => (IExecutionStep)new LayerStep(l.Nodes)).ToList();

            await ExecuteStepsAsync(
                steps, connections, nodeMap, context, invoker, feedbackContext,
                options, () => breakExecution, token).ConfigureAwait(false);
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
                feedbackContext.FeedbackInfo -= FeedbackHandler;
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

        var plan = _planner.BuildHierarchicalPlan(group.Nodes, group.Connections);
        var nodeMap = group.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var invoker = new NodeMethodInvoker(nodeContext, _typeResolver);
        var feedbackContext = nodeContext as INodeMethodContext;
        var effectiveOptions = options ?? NodeExecutionOptions.Default;

        await ExecuteStepsAsync(
            plan.Steps, group.Connections, nodeMap, childContext, invoker, feedbackContext,
            effectiveOptions, () => false, token).ConfigureAwait(false);

        foreach (var mapping in group.OutputMappings)
        {
            var value = childContext.GetSocketValue(mapping.NodeId, mapping.SocketName);
            parentContext.SetSocketValue(group.Id, mapping.GroupSocketName, value);
        }
    }

    // ────────────────────────── Unified step executor ──────────────────────────

    private async Task ExecuteStepsAsync(
        IReadOnlyList<IExecutionStep> steps,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        NodeExecutionOptions options,
        Func<bool> shouldBreak,
        CancellationToken token)
    {
        foreach (var step in steps)
        {
            token.ThrowIfCancellationRequested();
            if (shouldBreak()) break;

            switch (step)
            {
                case LayerStep layer:
                    await ExecuteLayerAsync(layer, connections, nodeMap, context, invoker, feedbackContext, options, shouldBreak, token)
                        .ConfigureAwait(false);
                    break;

                case LoopStep loop:
                    await ExecuteLoopAsync(loop, connections, nodeMap, context, invoker, feedbackContext, options, shouldBreak, token)
                        .ConfigureAwait(false);
                    break;

                case BranchStep branch:
                    await ExecuteBranchAsync(branch, connections, nodeMap, context, invoker, feedbackContext, options, shouldBreak, token)
                        .ConfigureAwait(false);
                    break;

                case ParallelSteps parallel:
                    await ExecuteParallelStepsAsync(parallel, connections, nodeMap, context, invoker, feedbackContext, options, shouldBreak, token)
                        .ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task ExecuteLayerAsync(
        LayerStep layer,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        NodeExecutionOptions options,
        Func<bool> shouldBreak,
        CancellationToken token)
    {
        var layerEvent = new ExecutionLayer(layer.Nodes);
        LayerStarted?.Invoke(this, new ExecutionLayerEventArgs(layerEvent));

        if (layer.Nodes.Count == 1 || options.MaxDegreeOfParallelism <= 1)
        {
            // Sequential within layer
            foreach (var node in layer.Nodes)
            {
                token.ThrowIfCancellationRequested();
                if (shouldBreak()) break;

                await Gate.WaitAsync(token).ConfigureAwait(false);
                await ExecuteNodeAsync(node, connections, nodeMap, context, invoker, feedbackContext, token)
                    .ConfigureAwait(false);
            }
        }
        else
        {
            // Parallel within layer
            using var throttler = new SemaphoreSlim(Math.Max(1, options.MaxDegreeOfParallelism));
            var tasks = layer.Nodes.Select(async node =>
            {
                await throttler.WaitAsync(token).ConfigureAwait(false);
                try
                {
                    await Gate.WaitAsync(token).ConfigureAwait(false);
                    await ExecuteNodeAsync(node, connections, nodeMap, context, invoker, feedbackContext, token)
                        .ConfigureAwait(false);
                }
                finally
                {
                    throttler.Release();
                }
            });

            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        LayerCompleted?.Invoke(this, new ExecutionLayerEventArgs(layerEvent));
    }

    private async Task ExecuteLoopAsync(
        LoopStep loop,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        NodeExecutionOptions options,
        Func<bool> shouldBreak,
        CancellationToken token)
    {
        var bodyNodeIds = loop.BodyNodes.Select(n => n.Id).ToList();
        var iteration = 0;

        while (iteration < MaxLoopIterations)
        {
            token.ThrowIfCancellationRequested();
            if (shouldBreak()) break;

            // Execute the loop header node
            await Gate.WaitAsync(token).ConfigureAwait(false);
            await ExecuteNodeAsync(loop.Header, connections, nodeMap, context, invoker, feedbackContext, token)
                .ConfigureAwait(false);

            // Check which path was signaled
            var loopPath = context.GetSocketValue(loop.Header.Id, loop.LoopPathSocket) as ExecutionPath;
            var exitPath = context.GetSocketValue(loop.Header.Id, loop.ExitPathSocket) as ExecutionPath;

            if (exitPath?.IsSignaled == true)
            {
                // Loop complete — follow exit path
                break;
            }

            if (loopPath?.IsSignaled != true)
            {
                // Neither signaled — shouldn't happen, but break to be safe
                break;
            }

            // Execute the loop body
            if (loop.Body.Count > 0)
            {
                // Push a new generation so body data nodes re-execute each iteration
                context.PushGeneration();
                context.ClearExecutedForNodes(bodyNodeIds);

                await ExecuteStepsAsync(
                    loop.Body, connections, nodeMap, context, invoker, feedbackContext,
                    options, shouldBreak, token).ConfigureAwait(false);

                context.PopGeneration();
            }

            iteration++;
        }

        if (iteration >= MaxLoopIterations)
        {
            throw new InvalidOperationException(
                $"Loop '{loop.Header.Name}' exceeded maximum iteration limit ({MaxLoopIterations}). " +
                "This may indicate an infinite loop.");
        }
    }

    private async Task ExecuteParallelStepsAsync(
        ParallelSteps parallel,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        NodeExecutionOptions options,
        Func<bool> shouldBreak,
        CancellationToken token)
    {
        if (parallel.Steps.Count == 0) return;

        if (parallel.Steps.Count == 1 || options.MaxDegreeOfParallelism <= 1)
        {
            // Fall back to sequential if only one step or parallelism disabled
            await ExecuteStepsAsync(parallel.Steps, connections, nodeMap, context, invoker, feedbackContext, options, shouldBreak, token)
                .ConfigureAwait(false);
            return;
        }

        var tasks = parallel.Steps.Select(step =>
            ExecuteStepsAsync(
                new[] { step }, connections, nodeMap, context, invoker, feedbackContext,
                options, shouldBreak, token));

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private async Task ExecuteBranchAsync(
        BranchStep branch,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        NodeExecutionOptions options,
        Func<bool> shouldBreak,
        CancellationToken token)
    {
        // Execute the condition node
        await Gate.WaitAsync(token).ConfigureAwait(false);
        await ExecuteNodeAsync(branch.ConditionNode, connections, nodeMap, context, invoker, feedbackContext, token)
            .ConfigureAwait(false);

        // Find which branch was signaled
        foreach (var (socketName, branchSteps) in branch.Branches)
        {
            var path = context.GetSocketValue(branch.ConditionNode.Id, socketName) as ExecutionPath;
            if (path?.IsSignaled == true)
            {
                await ExecuteStepsAsync(
                    branchSteps, connections, nodeMap, context, invoker, feedbackContext,
                    options, shouldBreak, token).ConfigureAwait(false);
                break;
            }
        }
    }

    // ────────────────────────── Node execution ──────────────────────────

    private async Task ExecuteNodeAsync(
        NodeData node,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        CancellationToken token)
    {
        // Skip callable nodes whose exec-input was not signaled by any upstream node.
        // This ensures branch targets that weren't chosen don't execute.
        if (node.Callable && !node.ExecInit)
        {
            var execInputs = node.Inputs.Where(s => s.IsExecution).ToList();
            if (execInputs.Count > 0)
            {
                var hasSignaledInput = false;
                foreach (var execInput in execInputs)
                {
                    // Find the upstream connection that feeds this exec input
                    var upstream = connections.FirstOrDefault(c =>
                        c.InputNodeId == node.Id &&
                        c.InputSocketName == execInput.Name &&
                        c.IsExecution);

                    if (upstream is not null)
                    {
                        var upstreamPath = context.GetSocketValue(upstream.OutputNodeId, upstream.OutputSocketName) as ExecutionPath;
                        if (upstreamPath?.IsSignaled == true)
                        {
                            hasSignaledInput = true;
                            break;
                        }
                    }
                    else
                    {
                        // No upstream connection — treat as unconditionally reachable
                        hasSignaledInput = true;
                        break;
                    }
                }

                if (!hasSignaledInput)
                {
                    // Skip this node — its exec path was not activated
                    return;
                }
            }
        }

        NodeStarted?.Invoke(this, new NodeExecutionEventArgs(node));

        try
        {
            if (feedbackContext is not null)
                feedbackContext.CurrentProcessingNode = node;

            await ResolveInputsAsync(node, connections, nodeMap, context, invoker, feedbackContext, token)
                .ConfigureAwait(false);

            if (VariableNodeExecutor.IsVariableNode(node))
            {
                VariableNodeExecutor.Execute(node, context);
                NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
                return;
            }

            if (EventNodeExecutor.IsEventNode(node))
            {
                if (EventNodeExecutor.IsTriggerNode(node))
                {
                    await EventNodeExecutor.ExecuteTriggerAsync(node, context, token).ConfigureAwait(false);
                }
                else
                {
                    // Listener nodes are handled by the event bus; just mark executed
                    EventNodeExecutor.ExecuteListener(node, context);
                }
                NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
                return;
            }

            var binding = invoker.Resolve(node);
            if (binding is null)
            {
                throw new InvalidOperationException($"No method binding found for node '{node.Name}'.");
            }

            await invoker.InvokeAsync(node, binding, context, token).ConfigureAwait(false);

            NodeCompleted?.Invoke(this, new NodeExecutionEventArgs(node));
        }
        catch (Exception ex)
        {
            NodeFailed?.Invoke(this, new NodeExecutionFailedEventArgs(node, ex));
            throw;
        }
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
        var path = new Stack<string>();

        async Task ResolveNodeAsync(NodeData target)
        {
            if (!stack.Add(target.Id))
            {
                var cyclePath = path.Reverse().SkipWhile(id => id != target.Id).Concat(new[] { target.Id });
                var cycleDescription = string.Join(" -> ", cyclePath.Select(id =>
                    nodeMap.TryGetValue(id, out var n) ? $"{n.Name} ({id})" : id));
                throw new InvalidOperationException(
                    $"Circular dependency detected in graph: {cycleDescription}. " +
                    "Data-flow nodes cannot have circular dependencies.");
            }

            path.Push(target.Id);

            foreach (var input in target.Inputs.Where(i => !i.IsExecution))
            {
                token.ThrowIfCancellationRequested();

                if (!connectionsByInput.TryGetValue((target.Id, input.Name), out var inbound))
                    continue;

                foreach (var connection in inbound)
                {
                    if (!nodeMap.TryGetValue(connection.OutputNodeId, out var sourceNode))
                        continue;

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
            path.Pop();
        }

        await ResolveNodeAsync(node).ConfigureAwait(false);
    }

    // ────────────────────────── Event listener registration ──────────────────────────

    /// <summary>
    /// Scans the graph for Custom Event (listener) nodes and registers handlers on the event bus.
    /// When a Trigger Event fires, the corresponding listener handlers execute the listener's
    /// downstream execution path.
    /// </summary>
    private void RegisterEventListeners(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        IReadOnlyDictionary<string, NodeData> nodeMap,
        INodeExecutionContext context,
        NodeMethodInvoker invoker,
        INodeMethodContext? feedbackContext,
        NodeExecutionOptions options,
        CancellationToken token)
    {
        var listenerNodes = nodes.Where(EventNodeExecutor.IsListenerNode).ToList();
        if (listenerNodes.Count == 0) return;

        foreach (var listener in listenerNodes)
        {
            var eventId = EventNodeExecutor.GetEventId(listener);
            if (eventId is null) continue;

            // Capture for closure
            var capturedListener = listener;

            context.EventBus.Subscribe(eventId, async () =>
            {
                // Signal the listener's Exit path
                EventNodeExecutor.ExecuteListener(capturedListener, context);

                // Find the downstream nodes connected to Exit and execute them
                var exitConnections = connections
                    .Where(c => c.OutputNodeId == capturedListener.Id && c.OutputSocketName == "Exit" && c.IsExecution)
                    .ToList();

                foreach (var exitConn in exitConnections)
                {
                    if (nodeMap.TryGetValue(exitConn.InputNodeId, out var downstreamNode))
                    {
                        await ExecuteNodeAsync(downstreamNode, connections, nodeMap, context, invoker, feedbackContext, token)
                            .ConfigureAwait(false);
                    }
                }
            });
        }
    }
}
