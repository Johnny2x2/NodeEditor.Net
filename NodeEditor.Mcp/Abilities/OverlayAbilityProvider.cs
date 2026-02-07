using System.Text.Json;
using NodeEditor.Blazor.Models;
using NodeEditor.Blazor.Services;
using NodeEditor.Blazor.ViewModels;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for managing overlays (organization notes and sections).
/// </summary>
public sealed class OverlayAbilityProvider : IAbilityProvider
{
    private readonly INodeEditorState _state;

    public OverlayAbilityProvider(INodeEditorState state)
    {
        _state = state;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("overlay.add", "Add Overlay", "Organization",
            "Creates an organization overlay (note/section) on the canvas.",
            "Provide a title, optional body text, position, size, color, and opacity. " +
            "Overlays are background visual elements used to organize and annotate the graph.",
            [
                new("title", "string", "The title/label for the overlay."),
                new("body", "string", "Optional body text/notes.", Required: false, DefaultValue: ""),
                new("x", "number", "X position.", Required: false, DefaultValue: "0"),
                new("y", "number", "Y position.", Required: false, DefaultValue: "0"),
                new("width", "number", "Width of the overlay.", Required: false, DefaultValue: "300"),
                new("height", "number", "Height of the overlay.", Required: false, DefaultValue: "200"),
                new("color", "string", "Background color (e.g. '#3399ff').", Required: false, DefaultValue: "#3399ff"),
                new("opacity", "number", "Opacity from 0.0 to 1.0.", Required: false, DefaultValue: "0.15")
            ],
            ReturnDescription: "The created overlay's id and title."),

        new("overlay.remove", "Remove Overlay", "Organization",
            "Removes an overlay from the canvas.",
            "Provide the overlayId to remove.",
            [new("overlayId", "string", "The ID of the overlay to remove.")]),

        new("overlay.list", "List Overlays", "Organization",
            "Lists all overlays (notes/sections) on the canvas.",
            "Returns all overlays with their id, title, body, position, and size.",
            [],
            ReturnDescription: "Array of overlay objects."),

        new("overlay.get", "Get Overlay Details", "Organization",
            "Gets detailed information about a specific overlay.",
            "Provide the overlayId to get full details.",
            [new("overlayId", "string", "The ID of the overlay.")]),

        new("overlay.select", "Select Overlays", "Organization",
            "Selects one or more overlays on the canvas.",
            "Provide a list of overlay IDs to select.",
            [
                new("overlayIds", "string[]", "Array of overlay IDs to select."),
                new("clearExisting", "boolean", "Clear existing selection first.", Required: false, DefaultValue: "true")
            ]),

        new("overlay.remove_selected", "Remove Selected Overlays", "Organization",
            "Removes all currently selected overlays.",
            "No parameters required.",
            [])
    ];

    public Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(abilityId switch
        {
            "overlay.add" => AddOverlay(parameters),
            "overlay.remove" => RemoveOverlay(parameters),
            "overlay.list" => ListOverlays(),
            "overlay.get" => GetOverlay(parameters),
            "overlay.select" => SelectOverlays(parameters),
            "overlay.remove_selected" => RemoveSelected(),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        });
    }

    private AbilityResult AddOverlay(JsonElement p)
    {
        if (!p.TryGetProperty("title", out var titleEl))
            return new AbilityResult(false, "Missing required parameter 'title'.");

        var title = titleEl.GetString()!;
        var body = p.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
        var x = p.TryGetProperty("x", out var xEl) ? xEl.GetDouble() : 0;
        var y = p.TryGetProperty("y", out var yEl) ? yEl.GetDouble() : 0;
        var w = p.TryGetProperty("width", out var wEl) ? wEl.GetDouble() : 300;
        var h = p.TryGetProperty("height", out var hEl) ? hEl.GetDouble() : 200;
        var color = p.TryGetProperty("color", out var colorEl) ? colorEl.GetString() ?? "#3399ff" : "#3399ff";
        var opacity = p.TryGetProperty("opacity", out var opacityEl) ? opacityEl.GetDouble() : 0.15;

        var data = new OverlayData(
            Id: Guid.NewGuid().ToString("N"),
            Title: title,
            Body: body,
            Position: new Point2D(x, y),
            Size: new Size2D(w, h),
            Color: color,
            Opacity: opacity);

        var vm = new OverlayViewModel(data);
        _state.AddOverlay(vm);

        return new AbilityResult(true, $"Overlay '{title}' created.",
            Data: new { data.Id, data.Title });
    }

    private AbilityResult RemoveOverlay(JsonElement p)
    {
        if (!p.TryGetProperty("overlayId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'overlayId'.");

        _state.RemoveOverlay(idEl.GetString()!);
        return new AbilityResult(true, "Overlay removed.");
    }

    private AbilityResult ListOverlays()
    {
        var overlays = _state.Overlays.Select(o => new
        {
            o.Id,
            o.Title,
            o.Body,
            Position = new { X = o.Position.X, Y = o.Position.Y },
            Size = new { Width = o.Size.Width, Height = o.Size.Height },
            o.Color,
            o.Opacity,
            o.IsSelected
        }).ToList();

        return new AbilityResult(true, $"Found {overlays.Count} overlay(s).", Data: overlays);
    }

    private AbilityResult GetOverlay(JsonElement p)
    {
        if (!p.TryGetProperty("overlayId", out var idEl))
            return new AbilityResult(false, "Missing required parameter 'overlayId'.");

        var overlayId = idEl.GetString()!;
        var overlay = _state.Overlays.FirstOrDefault(o => o.Id == overlayId);
        if (overlay is null)
            return new AbilityResult(false, $"Overlay '{overlayId}' not found.");

        return new AbilityResult(true, Data: new
        {
            overlay.Id,
            overlay.Title,
            overlay.Body,
            Position = new { X = overlay.Position.X, Y = overlay.Position.Y },
            Size = new { Width = overlay.Size.Width, Height = overlay.Size.Height },
            overlay.Color,
            overlay.Opacity,
            overlay.IsSelected
        });
    }

    private AbilityResult SelectOverlays(JsonElement p)
    {
        if (!p.TryGetProperty("overlayIds", out var idsEl))
            return new AbilityResult(false, "Missing required parameter 'overlayIds'.");

        var ids = new List<string>();
        foreach (var id in idsEl.EnumerateArray())
            ids.Add(id.GetString()!);

        var clearExisting = !p.TryGetProperty("clearExisting", out var clearEl) || clearEl.GetBoolean();
        _state.SelectOverlays(ids, clearExisting);
        return new AbilityResult(true, $"Selected {ids.Count} overlay(s).");
    }

    private AbilityResult RemoveSelected()
    {
        var count = _state.SelectedOverlayIds.Count;
        _state.RemoveSelectedOverlays();
        return new AbilityResult(true, $"Removed {count} selected overlay(s).");
    }
}
