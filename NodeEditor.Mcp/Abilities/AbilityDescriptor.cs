namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Describes a single ability that the MCP server can perform.
/// Abilities are discoverable, self-documenting, and executable.
/// </summary>
public sealed record AbilityDescriptor(
    string Id,
    string Name,
    string Category,
    string Summary,
    string DetailedUsage,
    IReadOnlyList<AbilityParameter> Parameters,
    string? ReturnDescription = null,
    string? Source = null);

/// <summary>
/// Describes a parameter for an ability.
/// </summary>
public sealed record AbilityParameter(
    string Name,
    string Type,
    string Description,
    bool Required = true,
    string? DefaultValue = null);
