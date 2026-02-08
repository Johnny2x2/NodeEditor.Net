# NodeEditor.Mcp

Model Context Protocol (MCP) server for NodeEditorMax. This server exposes NodeEditor abilities and tools over MCP, supporting both **stdio** (standalone) and **HTTP/SSE** (embedded in WebHost) transports.

## What this server does
- Exposes a discovery-based ability system (nodes, connections, graph, execution, plugins, logging, overlays, catalog).
- Provides three MCP tools (`discover_abilities`, `get_ability_info`, `execute_ability`) that let clients explore and invoke abilities.
- **Standalone mode**: runs headless over stdio, hosting its own `NodeEditorState` in-process.
- **Embedded mode**: runs inside the Blazor WebHost over HTTP/SSE, bridging to the live editor session so MCP commands update the canvas in real time.

## Prerequisites
- .NET SDK 10.x

---

## Transport modes

### 1. Stdio (standalone)
Run the MCP server as its own process. It hosts an isolated `NodeEditorState` — changes are not reflected in the WebHost UI.

```
dotnet run --project NodeEditor.Mcp/NodeEditor.Mcp.csproj
```

Client configuration:

```json
{
  "mcpServers": {
    "nodeeditormax": {
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "NodeEditor.Mcp/NodeEditor.Mcp.csproj"
      ]
    }
  }
}
```

### 2. HTTP/SSE (embedded in WebHost)
When enabled, the MCP server runs inside the Blazor WebHost and operates on the **live editor session**. MCP commands add nodes, create connections, and execute graphs directly on the canvas the user sees in the browser.

#### Enable MCP in the WebHost
Edit `appsettings.json`:

```json
{
  "Mcp": {
    "Enabled": true,
    "RoutePattern": "/mcp"
  }
}
```

Then start the WebHost normally:

```
dotnet run --project NodeEditor.Blazor.WebHost/NodeEditor.Blazor.WebHost.csproj
```

The MCP endpoint will be available at `http://localhost:<port>/mcp`.

#### Generate an API key
MCP HTTP requests require an API key. Open the Node Editor in the browser, go to **Settings → MCP Settings**, and click **Generate New Key**. The panel also shows a ready-to-paste client config.

#### Client configuration (HTTP)

```json
{
  "mcpServers": {
    "nodeeditormax": {
      "url": "http://localhost:5000/mcp",
      "headers": {
        "X-API-Key": "<your-api-key>"
      }
    }
  }
}
```

Replace `5000` with the actual port and paste the key from the settings panel.

#### Important: open the editor first
The HTTP transport uses a **state bridge** to reach the active Blazor circuit. If no browser tab is open with the Node Editor, MCP calls will return an error:

> No active editor session. Open the Node Editor in the browser first.

Open the editor in a browser before issuing MCP commands.

---

## Core MCP tools
The server exposes three MCP tools via `NodeEditorTools`:

1. `discover_abilities`
   - Lists available abilities and categories.
   - Optional filters: `category`, `search`, `includePlugins`.

2. `get_ability_info`
   - Returns full details for a specific ability id, including parameters and usage.

3. `execute_ability`
   - Executes an ability by id with a JSON object of parameters.

## Standard usage flow
1. Discover: call `discover_abilities` (optionally with a category).
2. Inspect: call `get_ability_info` for the ability you want.
3. Execute: call `execute_ability` with the ability id and parameters JSON.

## Ability catalog
Abilities are grouped by category. Use `get_ability_info` for parameter details.

### Catalog
- `catalog.list`
- `catalog.categories`
- `catalog.get`

### Nodes
- `node.add`
- `node.remove`
- `node.list`
- `node.get`
- `node.move`
- `node.select`
- `node.select_all`
- `node.clear_selection`
- `node.remove_selected`
- `node.set_socket_value`

### Connections
- `connection.add`
- `connection.remove`
- `connection.list`
- `connection.list_for_node`
- `connection.remove_all_for_node`

### Graph
- `graph.save`
- `graph.load`
- `graph.export_json`
- `graph.import_json`
- `graph.clear`
- `graph.summary`
- `graph.variable_list`
- `graph.variable_add`
- `graph.variable_remove`
- `graph.event_list`
- `graph.event_add`
- `graph.event_remove`

### Organization (Overlays)
- `overlay.add`
- `overlay.remove`
- `overlay.list`
- `overlay.get`
- `overlay.select`
- `overlay.remove_selected`

### Execution
- `execution.run`
- `execution.run_json`
- `execution.stop`
- `execution.status`
- `execution.pause`
- `execution.resume`
- `execution.step`

### Plugins
- `plugin.list_loaded`
- `plugin.list_installed`
- `plugin.install`
- `plugin.uninstall`
- `plugin.enable`
- `plugin.disable`
- `plugin.reload`

### Logging
- `log.get`
- `log.get_all`
- `log.clear`
- `log.channels`

## Execution model and state
- **Stdio mode**: the MCP server hosts its own `NodeEditorState` in memory. Changes are local to the server process.
- **HTTP mode (embedded)**: the MCP server bridges to the active Blazor circuit's `NodeEditorState` via `INodeEditorStateBridge`. Ability calls mutate the live editor canvas.
- `execution.run_json` executes a graph without changing the canvas state.
- `graph.save` and `graph.load` read/write JSON files on the local file system.

## Architecture (HTTP mode)

```
Browser (Blazor circuit)
  └─ NodeEditorState (scoped)
       ▲
       │ Attach / Detach
       ▼
  INodeEditorStateBridge (singleton)
       ▲
       │ Delegates via Current
       ▼
  BridgedNodeEditorState
       ▲
       │ Used by
       ▼
  MCP Ability Providers
       ▲
       │ Called by
       ▼
  MCP HTTP endpoint (/mcp)
       ▲
       │ X-API-Key auth
       ▼
  MCP Client (Claude, etc.)
```

- `NodeEditorStateBridge` is a thread-safe singleton holding a reference to the active scoped `NodeEditorState`.
- `BridgedNodeEditorState` implements `INodeEditorState` and proxies every call through the bridge.
- When the browser tab opens, `Home.razor` attaches the circuit's state; when it closes, it detaches.
- `McpApiKeyMiddleware` validates the `X-API-Key` header before requests reach the MCP endpoint.

## Security
- API keys are generated cryptographically (32 random bytes, URL-safe Base64).
- Keys are stored on disk at `%LocalAppData%/NodeEditorMax/mcp-api-key.dat`.
- Validation uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.
- Keys can be revoked and regenerated from the Settings panel.

## Plugin abilities
Plugins can contribute abilities by implementing `IPluginAbilityContributor` in the plugin assembly.
When `discover_abilities` is called with `includePlugins = true`, the server scans loaded plugins and registers any contributors found.

## Build and publish
Build:

```
dotnet build NodeEditor.Mcp/NodeEditor.Mcp.csproj
```

Publish (example):

```
dotnet publish NodeEditor.Mcp/NodeEditor.Mcp.csproj -c Release -o ./artifacts/mcp
```

Then configure your client to run the published executable directly.

## Troubleshooting
- **Ability not found**: call `discover_abilities` and check the ability id spelling.
- **Missing parameters**: call `get_ability_info` for required fields.
- **Plugin abilities not showing**: ensure plugins are loaded, then call `discover_abilities` with `includePlugins = true`.

## Server info
- Name: NodeEditorMax
- Version: 1.0.0
