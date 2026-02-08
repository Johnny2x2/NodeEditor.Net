using System.Text.Json;
using NodeEditor.Net.Services.Logging;

namespace NodeEditor.Mcp.Abilities;

/// <summary>
/// Provides abilities for reading and managing application logs.
/// </summary>
public sealed class LoggingAbilityProvider : IAbilityProvider
{
    private readonly INodeEditorLogger _logger;

    public LoggingAbilityProvider(INodeEditorLogger logger)
    {
        _logger = logger;
    }

    public string Source => "Core";

    public IReadOnlyList<AbilityDescriptor> GetAbilities() =>
    [
        new("log.get", "Get Logs", "Logging",
            "Retrieves log entries from a specific channel.",
            "Provide the channel name and optional line count to limit results. " +
            "Default channels: 'Execution', 'Debug', 'Application'. " +
            "Lines are returned newest-first by default.",
            [
                new("channel", "string", "Log channel name (e.g. 'Execution', 'Debug', 'Application')."),
                new("lines", "number", "Number of most recent lines to return.", Required: false, DefaultValue: "50"),
                new("newestFirst", "boolean", "If true, newest entries come first.", Required: false, DefaultValue: "true")
            ],
            ReturnDescription: "Array of log entries with timestamp, level, message, and optional node info."),

        new("log.get_all", "Get All Logs", "Logging",
            "Retrieves log entries from all channels combined.",
            "Returns entries from all channels sorted by timestamp. " +
            "Provide an optional line count to limit results.",
            [
                new("lines", "number", "Number of most recent lines to return.", Required: false, DefaultValue: "100"),
                new("newestFirst", "boolean", "If true, newest entries come first.", Required: false, DefaultValue: "true")
            ],
            ReturnDescription: "Array of log entries from all channels."),

        new("log.clear", "Clear Logs", "Logging",
            "Clears log entries from a specific channel or all channels.",
            "Provide a channel name to clear just that channel, or omit to clear all.",
            [new("channel", "string", "Channel to clear. Omit to clear all.", Required: false)]),

        new("log.channels", "List Log Channels", "Logging",
            "Lists all available log channels.",
            "Returns the names of all registered log channels.",
            [],
            ReturnDescription: "Array of channel name strings.")
    ];

    public Task<AbilityResult> ExecuteAsync(string abilityId, JsonElement parameters, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(abilityId switch
        {
            "log.get" => GetLogs(parameters),
            "log.get_all" => GetAllLogs(parameters),
            "log.clear" => ClearLogs(parameters),
            "log.channels" => ListChannels(),
            _ => new AbilityResult(false, $"Unknown ability: {abilityId}")
        });
    }

    private AbilityResult GetLogs(JsonElement p)
    {
        if (!p.TryGetProperty("channel", out var channelEl))
            return new AbilityResult(false, "Missing required parameter 'channel'.",
                ErrorHint: "Available channels: Execution, Debug, Application. Use log.channels to list all.");

        var channel = channelEl.GetString()!;
        var lines = p.TryGetProperty("lines", out var linesEl) ? linesEl.GetInt32() : 50;
        var newestFirst = !p.TryGetProperty("newestFirst", out var nfEl) || nfEl.GetBoolean();

        var entries = _logger.GetEntries(channel);
        var result = newestFirst
            ? entries.Reverse().Take(lines)
            : entries.TakeLast(lines);

        var formatted = result.Select(e => new
        {
            Timestamp = e.Timestamp.ToString("HH:mm:ss.fff"),
            Level = e.Level.ToString(),
            e.Channel,
            e.Message,
            e.NodeId,
            e.NodeName
        }).ToList();

        return new AbilityResult(true, $"Retrieved {formatted.Count} log entries from '{channel}'.", Data: formatted);
    }

    private AbilityResult GetAllLogs(JsonElement p)
    {
        var lines = p.TryGetProperty("lines", out var linesEl) ? linesEl.GetInt32() : 100;
        var newestFirst = !p.TryGetProperty("newestFirst", out var nfEl) || nfEl.GetBoolean();

        var channels = new[] { LogChannels.Execution, LogChannels.Debug, LogChannels.Application };
        var allEntries = channels
            .SelectMany(ch => _logger.GetEntries(ch))
            .OrderByDescending(e => e.Timestamp);

        var result = newestFirst
            ? allEntries.Take(lines)
            : allEntries.Reverse().TakeLast(lines);

        var formatted = result.Select(e => new
        {
            Timestamp = e.Timestamp.ToString("HH:mm:ss.fff"),
            Level = e.Level.ToString(),
            e.Channel,
            e.Message,
            e.NodeId,
            e.NodeName
        }).ToList();

        return new AbilityResult(true, $"Retrieved {formatted.Count} log entries.", Data: formatted);
    }

    private AbilityResult ClearLogs(JsonElement p)
    {
        var channel = p.TryGetProperty("channel", out var chEl) ? chEl.GetString() : null;
        _logger.Clear(channel);
        return new AbilityResult(true, channel is null ? "All logs cleared." : $"Logs for '{channel}' cleared.");
    }

    private AbilityResult ListChannels()
    {
        var channels = new[] { LogChannels.Execution, LogChannels.Debug, LogChannels.Application };
        return new AbilityResult(true, Data: channels);
    }
}
