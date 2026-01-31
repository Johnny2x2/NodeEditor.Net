using System.Text.Json;

namespace NodeEditor.Blazor.Services.Plugins;

public sealed record class PluginManifest(
    string Id,
    string Name,
    string Version,
    string MinApiVersion,
    string? EntryAssembly,
    string? Category)
{
    public static PluginManifest? Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = File.ReadAllText(manifestPath);
        return JsonSerializer.Deserialize<PluginManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }
}
