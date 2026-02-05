namespace NodeEditor.Blazor.Services.Registry;

public sealed record class NodeCategoryGroup(
    string Name,
    IReadOnlyList<NodeDefinition> Nodes,
    IReadOnlyList<NodeCategoryGroup> Children);

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
        var root = new CategoryNode("Root");

        foreach (var definition in all)
        {
            var category = string.IsNullOrWhiteSpace(definition.Category) ? "General" : definition.Category;
            var segments = category
                .Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var current = root;
            foreach (var segment in segments.Length == 0 ? new[] { "General" } : segments)
            {
                current = current.GetOrAdd(segment);
            }

            current.Nodes.Add(definition);
        }

        var categories = root.Children
            .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
            .Select(child => child.ToGroup())
            .ToList();

        return new NodeCatalog(all, categories);
    }

    private sealed class CategoryNode
    {
        public CategoryNode(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public List<NodeDefinition> Nodes { get; } = new();

        public List<CategoryNode> Children { get; } = new();

        public CategoryNode GetOrAdd(string name)
        {
            var existing = Children.FirstOrDefault(child => child.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing;
            }

            var created = new CategoryNode(name);
            Children.Add(created);
            return created;
        }

        public NodeCategoryGroup ToGroup()
        {
            var orderedNodes = Nodes
                .OrderBy(node => node.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var orderedChildren = Children
                .OrderBy(child => child.Name, StringComparer.OrdinalIgnoreCase)
                .Select(child => child.ToGroup())
                .ToList();

            return new NodeCategoryGroup(Name, orderedNodes, orderedChildren);
        }
    }
}
