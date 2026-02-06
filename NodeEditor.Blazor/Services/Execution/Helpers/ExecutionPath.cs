using System.Runtime.Serialization;

namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Represents an execution path output/input for execution sockets.
/// </summary>
[Serializable]
public sealed class ExecutionPath : ISerializable
{
    public bool IsSignaled { get; private set; }

    public ExecutionPath()
    {
    }

    private ExecutionPath(SerializationInfo info, StreamingContext context)
    {
    }

    public void Signal()
    {
        IsSignaled = true;
    }

    public void GetObjectData(SerializationInfo info, StreamingContext context)
    {
    }
}
