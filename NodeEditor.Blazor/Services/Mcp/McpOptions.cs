namespace NodeEditor.Blazor.Services.Mcp;

/// <summary>
/// Configuration options for the embedded MCP server.
/// Bound from the "Mcp" section in appsettings.json.
/// </summary>
public sealed class McpOptions
{
    public const string SectionName = "Mcp";

    /// <summary>The route pattern to map MCP endpoints to (e.g. "/mcp").</summary>
    public string RoutePattern { get; set; } = "/mcp";
}
