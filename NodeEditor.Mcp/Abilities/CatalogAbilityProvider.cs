using System.Text.Json;
using NodeEditor.Net.Services.Registry;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for browsing the node catalog (available node definitions).
/// </summary>
public sealed class CatalogAbilityProvider : IAbilityProvider
{
    private readonly INodeRegistryService _registry;

    public CatalogAbilityProvider(INodeRegistryService registry)
    {
        _registry = registry;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("catalog.list", "List Node Definitions", "Catalog",
            "Lists all available node definitions that can be added to the graph.",
            "Returns the full catalog of node definitions organized by category. " +
            "Use the 'definitionId' from results to add nodes with node.add.",
            [new("search", "string", "Optional keyword to filter definitions.", Required: false)],
            ReturnDescription: "Array of node definitions with id, name, category, description, and socket specifications."),

        new("catalog.categories", "List Categories", "Catalog",
            "Lists all node categories in the catalog.",
            "Returns the hierarchical category tree showing how nodes are organized.",
            [],
            ReturnDescription: "Array of category names with node counts."),

        new("catalog.get", "Get Definition Details", "Catalog",
            "Gets detailed information about a specific node definition.",
            "Provide the definitionId to see full details including all input/output sockets.",
            [new("definitionId", "string", "The definition ID to look up.")],
            ReturnDescription: "Full definition details including all sockets and their types.")
    ];

    public Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        _registry.EnsureInitialized();

        return Task.FromResult(abilityId switch
        {
            "catalog.list" => ListDefinitions(parameters),
            "catalog.categories" => ListCategories(),
            "catalog.get" => GetDefinition(parameters),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        });
    }

    private AbilityResult ListDefinitions(JsonElement p)
    {
        var search = p.TryGetProperty("search", out var searchEl) ? searchEl.GetString() : null;
        var catalog = _registry.GetCatalog(search);

        var defs = catalog.All.Select(d => new
        {
            d.Id,
            d.Name,
            d.Category,
            d.Description,
            InputCount = d.Inputs.Count,
            OutputCount = d.Outputs.Count
        }).ToList();

        return new AbilityResult(true, $"Found {defs.Count} definition(s).", Data: defs);
    }

    private AbilityResult ListCategories()
    {
        var catalog = _registry.GetCatalog();

        static object MapGroup(NodeCategoryGroup g) => new
        {
            g.Name,
            NodeCount = g.Nodes.Count,
            Children = g.Children.Select(MapGroup).ToList()
        };

        var categories = catalog.Categories.Select(MapGroup).ToList();
        return new AbilityResult(true, $"Found {categories.Count} top-level category(s).", Data: categories);
    }

    private AbilityResult GetDefinition(JsonElement p)
    {
        if (!p.TryGetProperty("definitionId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'definitionId'.",
                ErrorHint: "Use catalog.list to discover available definition IDs.");

        var defId = idEl.GetString()!;
        var def = _registry.Definitions.FirstOrDefault(d =>
            d.Id.Equals(defId, StringComparison.OrdinalIgnoreCase));

        if (def is null)
            return new AbilityResult(false, $"Definition '{defId}' not found.",
                ErrorHint: "Use catalog.list or catalog.list with search to find definitions.");

        return new AbilityResult(true, Data: new
        {
            def.Id,
            def.Name,
            def.Category,
            def.Description,
            Inputs = def.Inputs.Select(s => new
            {
                s.Name,
                s.TypeName,
                s.IsExecution,
                EditorHint = s.EditorHint?.Kind.ToString()
            }).ToList(),
            Outputs = def.Outputs.Select(s => new
            {
                s.Name,
                s.TypeName,
                s.IsExecution
            }).ToList()
        });
    }
}
