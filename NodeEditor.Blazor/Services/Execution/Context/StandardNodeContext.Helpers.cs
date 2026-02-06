namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext
{
    [Node("Start", "Basic", "Basic", "Starts execution.", true, true)]
    public void Start(out ExecutionPath Exit)
    {
        ReportRunning();
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Marker", "Basic", "Basic", "Exec marker.", true)]
    public void Marker(ExecutionPath Enter, out ExecutionPath Exit)
    {
        ReportRunning();
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Consume", "Basic", "Basic", "Consumes a value.", true)]
    public void Consume(ExecutionPath Enter, object Value, out ExecutionPath Exit)
    {
        ReportRunning();
        Exit = new ExecutionPath();
        Exit.Signal();
    }

    [Node("Delay", "Basic", "Basic", "Delay execution.", true)]
    public Task Delay(ExecutionPath Enter, int DelayMs, CancellationToken token, out ExecutionPath Exit)
    {
        ReportRunning();
        Exit = new ExecutionPath();
        Exit.Signal();
        return Task.Delay(DelayMs, token);
    }
}
