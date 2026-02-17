using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using NodeEditor.Net.Services.Logging;
using NodeEditor.Mcp.Abilities;

namespace NodeEditor.Mcp.Tools;

/// <summary>
/// MCP tools that expose the discovery-based ability system to AI clients.
/// Three core tools: discover_abilities, get_ability_info, execute_ability.
/// </summary>
[McpServerToolType]
public sealed class NodeEditorTools
{
    private readonly AbilityRegistry _registry;
    private readonly PluginAbilityDiscovery _pluginDiscovery;
    private readonly INodeEditorLogger _logger;

    public NodeEditorTools(AbilityRegistry registry, PluginAbilityDiscovery pluginDiscovery, INodeEditorLogger logger)
    {
        _registry = registry;
        _pluginDiscovery = pluginDiscovery;
        _logger = logger;
    }

    [McpServerTool(Name = "discover_abilities")]
    [Description(
        "Discovers all available abilities in the NodeEditor. " +
        "Use this first to learn what actions you can perform. " +
        "Optionally filter by category or search keyword. " +
        "After discovering abilities, use 'get_ability_info' to learn how to use a specific ability, " +
        "then use 'execute_ability' to perform the action.")]
    public string DiscoverAbilities(
        [Description("Optional category to filter by (e.g. 'Nodes', 'Connections', 'Graph', 'Plugins', 'Execution', 'Logging', 'Organization', 'Catalog')")]
        string? category = null,
        [Description("Optional keyword to search across ability names, descriptions, and categories")]
        string? search = null,
        [Description("If true, also discover abilities from loaded plugins")]
        bool includePlugins = true)
    {
        var filter = !string.IsNullOrWhiteSpace(search) ? $"search=\"{search}\""
            : !string.IsNullOrWhiteSpace(category) ? $"category=\"{category}\""
            : "all";
        _logger.Log(LogChannels.Mcp, LogLevel.Info, $"▶ discover_abilities ({filter})");
        if (includePlugins)
        {
            _pluginDiscovery.DiscoverAndRegister();
        }

        IReadOnlyList<AbilityDescriptor> abilities;

        if (!string.IsNullOrWhiteSpace(search))
        {
            abilities = _registry.Search(search);
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            abilities = _registry.GetByCategory(category);
        }
        else
        {
            abilities = _registry.GetAll();
        }

        var categories = _registry.GetCategories();

        var result = new
        {
            TotalAbilities = _registry.GetAll().Count,
            AvailableCategories = categories,
            Abilities = abilities.Select(a => new
            {
                a.Id,
                a.Name,
                a.Category,
                a.Summary,
                ParameterCount = a.Parameters.Count,
                a.Source
            }).ToList(),
            Hint = "Use get_ability_info with the ability id to learn parameters and detailed usage. " +
                   "Then use execute_ability with the ability id and parameters to perform the action."
        };

        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "get_ability_info")]
    [Description(
        "Gets detailed information about a specific ability including all parameters, their types, " +
        "whether they are required, default values, and detailed usage instructions. " +
        "Call this before using execute_ability to understand the expected parameters.")]
    public string GetAbilityInfo(
        [Description("The ability ID to get information about (e.g. 'node.add', 'connection.add', 'graph.save')")]
        string abilityId)
    {
        _logger.Log(LogChannels.Mcp, LogLevel.Info, $"▶ get_ability_info \"{abilityId}\"");
        var ability = _registry.GetById(abilityId);

        if (ability is null)
        {
            _logger.Log(LogChannels.Mcp, LogLevel.Warning, $"  ✗ Ability \"{abilityId}\" not found");
            var suggestions = _registry.Search(abilityId);
            return JsonSerializer.Serialize(new
            {
                Error = $"Ability '{abilityId}' not found.",
                Suggestions = suggestions.Take(5).Select(s => new { s.Id, s.Name, s.Summary }).ToList(),
                Hint = "Use discover_abilities to list all available abilities."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var result = new
        {
            ability.Id,
            ability.Name,
            ability.Category,
            ability.Summary,
            ability.DetailedUsage,
            ability.ReturnDescription,
            ability.Source,
            Parameters = ability.Parameters.Select(p => new
            {
                p.Name,
                p.Type,
                p.Description,
                p.Required,
                p.DefaultValue
            }).ToList(),
            Example = BuildExample(ability)
        };

        _logger.Log(LogChannels.Mcp, LogLevel.Info, $"  ✓ get_ability_info → {ability.Name} ({ability.Category})");
        return JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true });
    }

    [McpServerTool(Name = "execute_ability")]
    [Description(
        "Executes an ability with the given parameters. " +
        "Use discover_abilities to find ability IDs and get_ability_info to learn parameters. " +
        "Parameters should be provided as a JSON object matching the ability's parameter specification.")]
    public async Task<string> ExecuteAbility(
        [Description("The ability ID to execute (e.g. 'node.add', 'connection.add', 'graph.save')")]
        string abilityId,
        [Description("JSON object containing the parameters for the ability. Use get_ability_info to learn required parameters.")]
        string? parametersJson = null,
        CancellationToken cancellationToken = default)
    {
        _logger.Log(LogChannels.Mcp, LogLevel.Info, $"▶ execute_ability \"{abilityId}\"");
        JsonElement parameters;
        try
        {
            parameters = string.IsNullOrWhiteSpace(parametersJson)
                ? JsonDocument.Parse("{}").RootElement
                : JsonDocument.Parse(parametersJson).RootElement;
        }
        catch (JsonException ex)
        {
            _logger.Log(LogChannels.Mcp, LogLevel.Error, $"  ✗ Invalid JSON for \"{abilityId}\": {ex.Message}");
            return JsonSerializer.Serialize(new
            {
                Success = false,
                Error = $"Invalid JSON parameters: {ex.Message}",
                Hint = $"Use get_ability_info for '{abilityId}' to see the expected parameter format."
            }, new JsonSerializerOptions { WriteIndented = true });
        }

        var result = await _registry.ExecuteAsync(abilityId, parameters, cancellationToken);

        if (result.Success)
            _logger.Log(LogChannels.Mcp, LogLevel.Info, $"  ✓ execute_ability \"{abilityId}\" → {result.Message}");
        else
            _logger.Log(LogChannels.Mcp, LogLevel.Error, $"  ✗ execute_ability \"{abilityId}\" failed: {result.Message}");

        var response = new
        {
            result.Success,
            result.Message,
            result.Data,
            result.ErrorHint
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true });
    }

    private static string BuildExample(AbilityDescriptor ability)
    {
        var exampleParams = new Dictionary<string, object>();
        foreach (var param in ability.Parameters.Where(p => p.Required))
        {
            exampleParams[param.Name] = param.Type switch
            {
                "number" => 0,
                "boolean" => false,
                "string[]" => new[] { "example" },
                _ => $"<{param.Name}>"
            };
        }

        return exampleParams.Count > 0
            ? JsonSerializer.Serialize(exampleParams)
            : "{}";
    }
}
