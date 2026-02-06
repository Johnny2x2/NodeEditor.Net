using System.Collections.Concurrent;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Execution;

public sealed partial class StandardNodeContext : INodeMethodContext, INodeContext
{
    private readonly ConcurrentDictionary<string, object> _state = new(StringComparer.Ordinal);

    public NodeData? CurrentProcessingNode { get; set; }

    public event Action<string, NodeData, ExecutionFeedbackType, object?, bool>? FeedbackInfo;

    private void ReportRunning(string message = "Running")
    {
        if (CurrentProcessingNode is null)
        {
            return;
        }

        FeedbackInfo?.Invoke(message, CurrentProcessingNode, ExecutionFeedbackType.None, null, false);
    }

    private string GetStateKey(string suffix)
    {
        var id = CurrentProcessingNode?.Id ?? "node";
        return $"{id}:{suffix}";
    }

    private bool TryGetState<T>(string key, out T value)
    {
        if (_state.TryGetValue(key, out var stored) && stored is T typed)
        {
            value = typed;
            return true;
        }

        value = default!;
        return false;
    }

    private void SetState(string key, object value) => _state[key] = value;

    private void ClearState(string key) => _state.TryRemove(key, out _);
}
