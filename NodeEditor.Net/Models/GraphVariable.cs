namespace NodeEditor.Net.Models;

/// <summary>
/// Represents a user-defined graph variable.
/// Variables are graph-scoped named values that can be read/written by Get/Set Variable nodes.
/// Multiple nodes can reference the same variable and stay in sync through the execution context.
/// </summary>
public sealed record class GraphVariable(
    string Id,
    string Name,
    string TypeName,
    SocketValue? DefaultValue = null)
{
    /// <summary>
    /// Well-known definition ID prefix for Get Variable nodes.
    /// </summary>
    public const string GetDefinitionPrefix = "variable.get.";

    /// <summary>
    /// Well-known definition ID prefix for Set Variable nodes.
    /// </summary>
    public const string SetDefinitionPrefix = "variable.set.";

    /// <summary>
    /// Gets the node definition ID for the Get Variable node of this variable.
    /// </summary>
    public string GetDefinitionId => GetDefinitionPrefix + Id;

    /// <summary>
    /// Gets the node definition ID for the Set Variable node of this variable.
    /// </summary>
    public string SetDefinitionId => SetDefinitionPrefix + Id;

    /// <summary>
    /// Creates a new variable with a generated ID.
    /// </summary>
    public static GraphVariable Create(string name, string typeName, SocketValue? defaultValue = null)
    {
        return new GraphVariable(Guid.NewGuid().ToString("N"), name, typeName, defaultValue);
    }
}
