namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Marks a method as a node handler for execution binding.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NodeAttribute : Attribute
{
    public NodeAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
