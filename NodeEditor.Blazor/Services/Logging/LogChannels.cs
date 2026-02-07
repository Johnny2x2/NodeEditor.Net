namespace NodeEditor.Blazor.Services.Logging;

/// <summary>
/// Well-known built-in log channel names.
/// </summary>
public static class LogChannels
{
    /// <summary>
    /// Execution trace channel — logs node start/complete/fail events.
    /// Cleared automatically on each execution run.
    /// </summary>
    public const string Execution = "Execution";

    /// <summary>
    /// Debug channel — logs output from Debug Print nodes and FeedbackInfo messages.
    /// Cleared automatically on each execution run.
    /// </summary>
    public const string Debug = "Debug";

    /// <summary>
    /// Application channel — logs graph save/load/export, plugin lifecycle, and general app events.
    /// Only cleared manually.
    /// </summary>
    public const string Application = "Application";
}
