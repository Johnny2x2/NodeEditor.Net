using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services.Registry;

namespace NodeEditor.Blazor.Services.Execution;

public sealed class CompositeNodeContext : INodeMethodContext, INodeContext, INodeContextHost
{
    private readonly List<object> _contexts;
    private NodeData? _currentProcessingNode;

    public CompositeNodeContext(IEnumerable<object> contexts)
    {
        _contexts = contexts?.Where(context => context is not null).ToList() ?? new List<object>();

        foreach (var context in _contexts.OfType<INodeMethodContext>())
        {
            context.FeedbackInfo += ForwardFeedback;
        }
    }

    public IReadOnlyList<object> Contexts => _contexts;

    public NodeData? CurrentProcessingNode
    {
        get => _currentProcessingNode;
        set
        {
            _currentProcessingNode = value;
            foreach (var context in _contexts.OfType<INodeMethodContext>())
            {
                context.CurrentProcessingNode = value;
            }
        }
    }

    public event Action<string, NodeData, ExecutionFeedbackType, object?, bool>? FeedbackInfo;

    private void ForwardFeedback(string message, NodeData node, ExecutionFeedbackType type, object? tag, bool breakFlag)
    {
        FeedbackInfo?.Invoke(message, node, type, tag, breakFlag);
    }
}
