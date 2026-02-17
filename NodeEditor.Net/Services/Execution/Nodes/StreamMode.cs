namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Controls how streaming emissions interact with downstream execution.
/// </summary>
public enum StreamMode
{
    /// <summary>
    /// Each EmitAsync call waits for downstream nodes to complete before returning.
    /// The node processes items sequentially.
    /// </summary>
    Sequential,

    /// <summary>
    /// EmitAsync returns immediately. Downstream nodes run concurrently.
    /// The node continues producing items without waiting.
    /// </summary>
    FireAndForget
}
