namespace NodeEditor.Net.Models;

/// <summary>
/// Data carried during a variable drag-and-drop operation from the Variables panel to the canvas.
/// </summary>
public sealed record class VariableDragData(string VariableId, string VariableName);
