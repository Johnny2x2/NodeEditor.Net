namespace NodeEditor.Blazor.Services.Execution;

public sealed class ExecutionLayerEventArgs : EventArgs
{
    public ExecutionLayerEventArgs(ExecutionLayer layer)
    {
        Layer = layer;
    }

    public ExecutionLayer Layer { get; }
}
