namespace NodeEditor.Blazor.Services.Execution;

/// <summary>
/// Marks a method as a node handler for execution binding and discovery.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class NodeAttribute : Attribute
{
    public NodeAttribute(string name)
        : this(name, "", "General", "Some node.", true, false)
    {
    }

    public NodeAttribute(
        string name = "Node",
        string menu = "",
        string category = "General",
        string description = "Some node.",
        bool isCallable = true,
        bool isExecutionInitiator = false)
    {
        Name = name;
        Menu = menu;
        Category = category;
        Description = description;
        IsCallable = isCallable;
        IsExecutionInitiator = isExecutionInitiator;
    }

    public string Name { get; }

    public string Menu { get; }

    public string Category { get; }

    public string Description { get; }

    public bool IsCallable { get; }

    public bool IsExecutionInitiator { get; }
}
