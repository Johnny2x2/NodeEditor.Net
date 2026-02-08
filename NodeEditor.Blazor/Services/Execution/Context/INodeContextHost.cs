namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Provides access to multiple node contexts for execution binding.
/// </summary>
public interface INodeContextHost
{
    IReadOnlyList<object> Contexts { get; }
}
