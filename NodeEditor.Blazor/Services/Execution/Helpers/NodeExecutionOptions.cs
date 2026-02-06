namespace NodeEditor.Blazor.Services.Execution;

public sealed record NodeExecutionOptions(
    ExecutionMode Mode,
    bool AllowBackground,
    int MaxDegreeOfParallelism)
{
    public static NodeExecutionOptions Default { get; } = new(
        ExecutionMode.Sequential,
        AllowBackground: false,
        MaxDegreeOfParallelism: Environment.ProcessorCount);
}
