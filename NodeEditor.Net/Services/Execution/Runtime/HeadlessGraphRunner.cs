using NodeEditor.Net.Models;
using NodeEditor.Net.Services.Serialization;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Executes a GraphData directly without any UI state, ViewModels, or Blazor components.
/// </summary>
public sealed class HeadlessGraphRunner
{
    private readonly INodeExecutionService _executionService;
    private readonly IGraphSerializer _serializer;
    private readonly ISocketTypeResolver? _typeResolver;

    public HeadlessGraphRunner(
        INodeExecutionService executionService,
        IGraphSerializer serializer,
        ISocketTypeResolver? typeResolver = null)
    {
        _executionService = executionService ?? throw new ArgumentNullException(nameof(executionService));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _typeResolver = typeResolver;
    }

    /// <summary>
    /// Execute a graph from its pure model representation.
    /// </summary>
    public async Task<INodeRuntimeStorage> ExecuteAsync(
        GraphData graphData,
        INodeRuntimeStorage? context = null,
        object? nodeContext = null,
        NodeExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (graphData is null)
        {
            throw new ArgumentNullException(nameof(graphData));
        }

        var nodes = graphData.Nodes.Select(n => n.Data).ToList();
        var connections = graphData.Connections.ToList();
        var executionContext = context ?? new NodeExecutionContext();
        var effectiveNodeContext = nodeContext ?? new NodeContextFactory().CreateCompositeFromLoadedAssemblies();

        VariableNodeExecutor.SeedVariables(executionContext, graphData.Variables, _typeResolver);

        await _executionService.ExecuteAsync(
            nodes,
            connections,
            executionContext,
            effectiveNodeContext,
            options,
            cancellationToken);

        return executionContext;
    }

    /// <summary>
    /// Convenience: load from JSON and execute.
    /// </summary>
    public Task<INodeRuntimeStorage> ExecuteFromJsonAsync(
        string json,
        INodeRuntimeStorage? context = null,
        object? nodeContext = null,
        NodeExecutionOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var graphData = _serializer.DeserializeToGraphData(json);
        return ExecuteAsync(graphData, context, nodeContext, options, cancellationToken);
    }
}
