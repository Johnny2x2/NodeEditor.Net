namespace NodeEditor.Blazor.Services.Registry;

public sealed record class NodeCategoryGroup(string Name, IReadOnlyList<NodeDefinition> Nodes);

public sealed class NodeCatalog
{
    public NodeCatalog(IReadOnlyList<NodeDefinition> all, IReadOnlyList<NodeCategoryGroup> categories)
    {
        All = all;
        Categories = categories;
    }

    public IReadOnlyList<NodeDefinition> All { get; }

    public IReadOnlyList<NodeCategoryGroup> Categories { get; }

    public static NodeCatalog Create(IEnumerable<NodeDefinition> definitions)
    {
        var all = definitions.ToList();
        var categories = all
            .GroupBy(definition => string.IsNullOrWhiteSpace(definition.Category) ? "General" : definition.Category)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new NodeCategoryGroup(
                group.Key,
                group.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList()))
            .ToList();

        return new NodeCatalog(all, categories);
    }
}
