namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Controls execution flow for step-debugging. The gate can be open (free-running),
/// closed (paused), or advanced one step at a time.
/// </summary>
public sealed class ExecutionGate : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(0, int.MaxValue);
    private volatile bool _isOpen = true;
    private volatile bool _disposed;

    /// <summary>
    /// Current execution state.
    /// </summary>
    public ExecutionState State { get; private set; } = ExecutionState.Idle;

    /// <summary>
    /// Raised when <see cref="State"/> changes.
    /// </summary>
    public event EventHandler? StateChanged;

    /// <summary>
    /// If the gate is open, returns immediately. If closed (paused),
    /// blocks until <see cref="StepOnce"/> or <see cref="Resume"/> is called.
    /// </summary>
    public async Task WaitAsync(CancellationToken token)
    {
        if (_disposed) return;
        if (_isOpen) return;
        await _semaphore.WaitAsync(token).ConfigureAwait(false);
    }

    /// <summary>
    /// Start execution in free-running mode.
    /// </summary>
    public void Run()
    {
        _isOpen = true;
        SetState(ExecutionState.Running);
    }

    /// <summary>
    /// Start execution in paused mode (step-by-step).
    /// </summary>
    public void StartPaused()
    {
        _isOpen = false;
        DrainSemaphore();
        SetState(ExecutionState.Paused);
    }

    /// <summary>
    /// Pause a running execution. The next gate wait will block.
    /// </summary>
    public void Pause()
    {
        _isOpen = false;
        DrainSemaphore();
        SetState(ExecutionState.Paused);
    }

    /// <summary>
    /// Resume a paused execution to free-running.
    /// </summary>
    public void Resume()
    {
        _isOpen = true;
        // Release any currently-waiting step
        try { _semaphore.Release(); } catch (SemaphoreFullException) { }
        SetState(ExecutionState.Running);
    }

    /// <summary>
    /// Advance exactly one step, then re-pause.
    /// </summary>
    public void StepOnce()
    {
        if (State != ExecutionState.Paused) return;
        // Release the semaphore for exactly one waiter
        try { _semaphore.Release(); } catch (SemaphoreFullException) { }
    }

    /// <summary>
    /// Mark execution as complete.
    /// </summary>
    public void Complete()
    {
        _isOpen = true;
        SetState(ExecutionState.Idle);
    }

    public void Dispose()
    {
        _disposed = true;
        _isOpen = true;
        try { _semaphore.Release(); } catch (SemaphoreFullException) { }
        _semaphore.Dispose();
    }

    private void SetState(ExecutionState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    private void DrainSemaphore()
    {
        while (_semaphore.CurrentCount > 0)
        {
            _semaphore.Wait(0);
        }
    }
}

/// <summary>
/// Represents the current state of execution.
/// </summary>
public enum ExecutionState
{
    Idle,
    Running,
    Paused
}
