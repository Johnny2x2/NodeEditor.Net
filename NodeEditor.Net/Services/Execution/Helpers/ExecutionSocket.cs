namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Marker type for execution sockets. Unlike the old ExecutionPath (which carried
/// IsSignaled state), this is purely a type marker used in SocketData.TypeName
/// to distinguish execution sockets from data sockets. Flow control is handled
/// by TriggerAsync() on INodeExecutionContext, not by signaling objects.
/// </summary>
public static class ExecutionSocket
{
    public static readonly string TypeName = "NodeEditor.Net.Services.Execution.ExecutionSocket";
}
