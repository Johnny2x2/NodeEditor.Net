using System.Text.Json;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Central registry that aggregates ability providers and routes ability execution.
/// </summary>
public sealed class AbilityRegistry
{
    private readonly List<IAbilityProvider> _providers = new();
    private readonly object _lock = new();

    /// <summary>Registers an ability provider.</summary>
    public void Register(IAbilityProvider provider)
    {
        lock (_lock)
        {
            _providers.Add(provider);
        }
    }

    /// <summary>Returns all registered abilities across all providers.</summary>
    public IReadOnlyList<AbilityDescriptor> GetAll()
    {
        lock (_lock)
        {
            return _providers.SelectMany(p => p.GetAbilities()).ToList();
        }
    }

    /// <summary>Returns abilities filtered by category.</summary>
    public IReadOnlyList<AbilityDescriptor> GetByCategory(string category)
    {
        return GetAll()
            .Where(a => a.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Returns a specific ability by its unique id.</summary>
    public AbilityDescriptor? GetById(string abilityId)
    {
        return GetAll()
            .FirstOrDefault(a => a.Id.Equals(abilityId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Returns all known categories.</summary>
    public IReadOnlyList<string> GetCategories()
    {
        return GetAll()
            .Select(a => a.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>Searches abilities by keyword across name, summary, and category.</summary>
    public IReadOnlyList<AbilityDescriptor> Search(string keyword)
    {
        return GetAll()
            .Where(a =>
                a.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                a.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                a.Category.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                a.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>Executes an ability by its id, routing to the correct provider.</summary>
    public async Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        IAbilityProvider? provider;
        lock (_lock)
        {
            provider = _providers.FirstOrDefault(p =>
                p.GetAbilities().Any(a => a.Id.Equals(abilityId, StringComparison.OrdinalIgnoreCase)));
        }

        if (provider is null)
        {
            var suggestions = Search(abilityId);
            var hint = suggestions.Count > 0
                ? $"Did you mean one of: {string.Join(", ", suggestions.Take(5).Select(s => s.Id))}?"
                : "Use discover_abilities to list available abilities.";
            return new AbilityResult(false, $"Ability '{abilityId}' not found.", ErrorHint: hint);
        }

        try
        {
            return await provider.ExecuteAsync(abilityId, parameters, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return new AbilityResult(false, $"Ability execution failed: {ex.Message}",
                ErrorHint: "Check the parameters and try again. Use get_ability_info to see required parameters.");
        }
    }
}
