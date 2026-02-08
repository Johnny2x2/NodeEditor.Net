using System.Text.Json;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Interface for an ability provider that contributes discoverable abilities.
/// Plugins can implement this to extend the MCP server dynamically.
/// </summary>
public interface IAbilityProvider
{
    /// <summary>Source name shown to the AI (e.g. "Core", "MyPlugin").</summary>
    string Source { get; }

    /// <summary>Returns the abilities this provider exposes.</summary>
    IReadOnlyList<AbilityDescriptor> GetAbilities();

    /// <summary>Executes an ability by its id with the given JSON parameters.</summary>
    Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of executing an ability.
/// </summary>
public sealed record AbilityResult(
    bool Success,
    string? Message = null,
    object? Data = null,
    string? ErrorHint = null);
