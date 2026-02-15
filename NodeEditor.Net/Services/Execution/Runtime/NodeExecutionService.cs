using NodeEditor.Net.Models;
using NodeEditor.Net.Services;
using NodeEditor.Net.Services.Plugins;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Net.Services.Execution;

public sealed class NodeExecutionService : INodeExecutionService
{
    private readonly ExecutionPlanner _planner;
    private readonly INodeRegistryService _registry;
    private readonly IServiceProvider _services;
    private readonly IPluginLoader? _pluginLoader;
    private readonly IPluginServiceRegistry? _pluginServiceRegistry;

    public NodeExecutionService(
        ExecutionPlanner planner,
        INodeRegistryService registry,
        IServiceProvider services,
        IPluginLoader? pluginLoader = null,
        IPluginServiceRegistry? pluginServiceRegistry = null)
    {
        _planner = planner;
        _registry = registry;
        _services = services;
        _pluginLoader = pluginLoader;
        _pluginServiceRegistry = pluginServiceRegistry;
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

    //  Public entry points 

    public async Task ExecuteAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeRuntimeStorage context,
        object nodeContext,
        NodeExecutionOptions? options,
        CancellationToken token)
    {
        var effectiveOptions = options ?? NodeExecutionOptions.Default;
        await ExecuteGraphAsync(nodes, connections, context, effectiveOptions, token)
            .ConfigureAwait(false);
    }

    public async Task ExecuteGroupAsync(
        GroupNodeData group,
        INodeRuntimeStorage parentContext,
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

        var effectiveOptions = options ?? NodeExecutionOptions.Default;

        await ExecuteGraphAsync(group.Nodes, group.Connections, childContext, effectiveOptions, token)
            .ConfigureAwait(false);

        foreach (var mapping in group.OutputMappings)
        {
            var value = childContext.GetSocketValue(mapping.NodeId, mapping.SocketName);
            parentContext.SetSocketValue(group.Id, mapping.GroupSocketName, value);
        }
    }

    //  Graph execution 

    private async Task ExecuteGraphAsync(
        IReadOnlyList<NodeData> nodes,
        IReadOnlyList<ConnectionData> connections,
        INodeRuntimeStorage context,
        NodeExecutionOptions options,
        CancellationToken token)
    {
        var validation = _planner.ValidateGraph(nodes, connections);
        if (validation.HasErrors)
        {
            var details = string.Join(" ", validation.Messages
                .Where(m => m.Severity == ValidationSeverity.Error)
                .Select(m => m.Message));
            throw new InvalidOperationException(details.Length == 0
                ? "Graph validation failed."
                : details);
        }

        // 1. Build runtime
        var runtime = new ExecutionRuntime(nodes, connections, context,
            _services, _registry, Gate, options, token, ResolveServicesForDefinition);

        // Forward runtime events to service events
        runtime.NodeStarted += (s, e) => NodeStarted?.Invoke(this, e);
        runtime.NodeCompleted += (s, e) => NodeCompleted?.Invoke(this, e);
        runtime.NodeFailed += (s, e) => NodeFailed?.Invoke(this, e);
        runtime.FeedbackReceived += (s, e) => FeedbackReceived?.Invoke(this, e);

        // 2. Register event listeners (Custom Event nodes)
        RegisterEventListeners(runtime, nodes, context);

        // 3. Create all node instances (for DI setup)
        foreach (var node in nodes)
        {
            var instance = runtime.GetOrCreateInstance(node.Id);
            if (instance is not null)
                await instance.OnCreatedAsync(runtime.GetServicesForNode(node.Id)).ConfigureAwait(false);
        }

        // 4. Find initiator nodes and execute them
        var initiators = nodes.Where(n => n.ExecInit).ToList();

        try
        {
            if (options.MaxDegreeOfParallelism > 1 && initiators.Count > 1)
            {
                await Task.WhenAll(initiators.Select(n => runtime.ExecuteNodeByIdAsync(n.Id)))
                    .ConfigureAwait(false);
            }
            else
            {
                foreach (var initiator in initiators)
                    await runtime.ExecuteNodeByIdAsync(initiator.Id).ConfigureAwait(false);
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
            // 5. Cleanup
            foreach (var (_, instance) in runtime.NodeInstances)
                instance?.OnDisposed();
        }
    }

    //  Event listener registration 

    /// <summary>
    /// Scans the graph for Custom Event (listener) nodes and registers handlers on the event bus.
    /// When a Trigger Event fires, the corresponding listener handlers execute the listener's
    /// downstream execution path via the runtime.
    /// </summary>
    private static void RegisterEventListeners(
        ExecutionRuntime runtime,
        IReadOnlyList<NodeData> nodes,
        INodeRuntimeStorage context)
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

                // Find downstream nodes connected to Exit and execute them via runtime
                var exitTargets = runtime.GetExecutionTargets(capturedListener.Id, "Exit");
                foreach (var (targetNodeId, _) in exitTargets)
                {
                    await runtime.ExecuteNodeByIdAsync(targetNodeId).ConfigureAwait(false);
                }
            });
        }
    }

    private IServiceProvider? ResolveServicesForDefinition(string definitionId)
    {
        if (string.IsNullOrWhiteSpace(definitionId)
            || _pluginLoader is null
            || _pluginServiceRegistry is null)
        {
            return null;
        }

        var plugin = _pluginLoader.GetPluginForDefinition(definitionId);
        if (plugin is null)
        {
            return null;
        }

        return _pluginServiceRegistry.TryGetServices(plugin.Value.PluginId, out var services)
            ? services
            : null;
    }
}
