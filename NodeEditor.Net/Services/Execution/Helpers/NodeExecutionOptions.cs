namespace NodeEditor.Net.Services.Execution;

public sealed record NodeExecutionOptions(
    ExecutionMode Mode,
    bool AllowBackground,
    int MaxDegreeOfParallelism,
    StreamMode StreamMode = StreamMode.Sequential,
    int MaxCallDepth = 512)
{
    public static NodeExecutionOptions Default { get; } = new(
        ExecutionMode.Sequential,
        AllowBackground: false,
        MaxDegreeOfParallelism: Environment.ProcessorCount);
}
