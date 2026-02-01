namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext
{
    [Node("Parallel Split", "Parallel", "Basic", "Signal multiple execution paths.", false)]
    public void ParallelSplit(ExecutionPath Start, out ExecutionPath PathA, out ExecutionPath PathB)
    {
        ReportRunning();
        PathA = new ExecutionPath();
        PathB = new ExecutionPath();

        if (Start?.IsSignaled != false)
        {
            PathA.Signal();
            PathB.Signal();
        }
    }

    [Node("Parallel Join", "Parallel", "Basic", "Wait for multiple paths to complete.", false)]
    public void ParallelJoin(ExecutionPath PathA, ExecutionPath PathB, out ExecutionPath Exit)
    {
        ReportRunning();
        Exit = new ExecutionPath();

        var key = GetStateKey("parallel-join");
        var mask = 0;
        if (TryGetState(key, out int stored))
        {
            mask = stored;
        }

        if (PathA?.IsSignaled == true)
        {
            mask |= 1;
        }

        if (PathB?.IsSignaled == true)
        {
            mask |= 2;
        }

        SetState(key, mask);

        if (mask == 3)
        {
            ClearState(key);
            Exit.Signal();
        }
    }
}
