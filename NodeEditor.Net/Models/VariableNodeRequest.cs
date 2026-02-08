namespace NodeEditor.Net.Models;

/// <summary>
/// Request to create a variable node in the graph.
/// </summary>
public sealed record class VariableNodeRequest(string VariableId, bool IsSetNode);
