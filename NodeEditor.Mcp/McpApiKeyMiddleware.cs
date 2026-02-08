using Microsoft.AspNetCore.Http;
using NodeEditor.Blazor.Services.Logging;
using NodeEditor.Blazor.Services.Mcp;

namespace NodeEditor.Mcp;

/// <summary>
/// ASP.NET Core middleware that validates the <c>X-API-Key</c> header on MCP routes.
/// Returns 401 Unauthorized if the key is missing or invalid.
/// Logs all MCP request activity to the MCP output channel.
/// </summary>
public sealed class McpApiKeyMiddleware
{
    public const string ApiKeyHeaderName = "X-API-Key";

    private readonly RequestDelegate _next;
    private readonly McpApiKeyService _apiKeyService;
    private readonly INodeEditorLogger _logger;
    private readonly string _mcpRoutePrefix;

    public McpApiKeyMiddleware(
        RequestDelegate next,
        McpApiKeyService apiKeyService,
        INodeEditorLogger logger,
        string mcpRoutePrefix)
    {
        _next = next;
        _apiKeyService = apiKeyService;
        _logger = logger;
        _mcpRoutePrefix = mcpRoutePrefix.TrimEnd('/');
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Only gate requests to the MCP route
        if (path.StartsWith(_mcpRoutePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var method = context.Request.Method;
            _logger.Log(LogChannels.Mcp, LogLevel.Info, $"⇒ {method} {path}");

            // If MCP is disabled at runtime, reject all requests
            if (!_apiKeyService.IsEnabled())
            {
                _logger.Log(LogChannels.Mcp, LogLevel.Warning, $"✗ Rejected — server disabled ({method} {path})");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync(
                    "MCP server is disabled. Enable it from the Node Editor settings.");
                return;
            }

            // If no key has been generated yet, reject all MCP requests
            if (_apiKeyService.GetCurrentKey() is null)
            {
                _logger.Log(LogChannels.Mcp, LogLevel.Warning, $"✗ Rejected — no API key generated ({method} {path})");
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                await context.Response.WriteAsync(
                    "MCP API key has not been generated yet. Open Settings in the Node Editor UI to create one.");
                return;
            }

            var providedKey = context.Request.Headers[ApiKeyHeaderName].FirstOrDefault();
            if (!_apiKeyService.ValidateKey(providedKey))
            {
                _logger.Log(LogChannels.Mcp, LogLevel.Warning, $"✗ Unauthorized — invalid API key ({method} {path})");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid or missing API key. Provide a valid X-API-Key header.");
                return;
            }

            _logger.Log(LogChannels.Mcp, LogLevel.Info, $"✓ Authenticated ({method} {path})");

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _next(context);
            sw.Stop();

            _logger.Log(LogChannels.Mcp, LogLevel.Info,
                $"⇐ {context.Response.StatusCode} ({sw.ElapsedMilliseconds}ms) {method} {path}");
            return;
        }

        await _next(context);
    }
}
