namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Defines the execution mode for node graph execution.
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Sequential execution following control-flow connections.
    /// Only executes nodes reachable from ExecInit or Callable entry points.
    /// </summary>
    Sequential,
    
    /// <summary>
    /// Parallel execution using topological layers.
    /// Executes nodes layer-by-layer based on data dependencies.
    /// </summary>
    Parallel,
    
    /// <summary>
    /// Data-flow execution mode for pure computational graphs.
    /// Executes all nodes in topological order based on data dependencies,
    /// regardless of Callable or ExecInit flags.
    /// </summary>
    DataFlow
}
