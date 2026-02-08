using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

/// <summary>
/// Optional interface for node method contexts that need execution metadata.
/// </summary>
public interface INodeMethodContext
{
    NodeData? CurrentProcessingNode { get; set; }

    event Action<string, NodeData, ExecutionFeedbackType, object?, bool>? FeedbackInfo;
}
