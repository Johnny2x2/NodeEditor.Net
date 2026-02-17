using NodeEditor.Net.Models;

namespace NodeEditor.Net.Services.Execution;

public sealed record ExecutionLayer(IReadOnlyList<NodeData> Nodes);
