# MCP State Bridge

The MCP (Model Context Protocol) state bridge is a pattern that connects the singleton MCP server to the scoped Blazor circuit's `NodeEditorState`, enabling AI assistants to control the live canvas in real-time.

## The Problem

In Blazor Server, `NodeEditorState` is a **scoped** service—one instance per user circuit. The MCP HTTP/SSE endpoint, however, is a **singleton** that lives outside of any Blazor circuit. The MCP server needs to read and modify the user's state, but it can't directly inject a scoped service.

## The Solution: State Bridge Pattern

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
  MCP Client (Claude, Cursor, etc.)
```

### Key Components

| Component | Lifetime | Role |
|-----------|----------|------|
| `INodeEditorStateBridge` | Singleton | Holds a reference to the active circuit's state |
| `NodeEditorStateBridge` | Singleton | Thread-safe implementation with Attach/Detach |
| `BridgedNodeEditorState` | Singleton | Implements `INodeEditorState` by proxying through the bridge |

### How It Works

1. **Browser opens**: When a user opens the Node Editor in their browser, the Blazor circuit initializes. The `Home.razor` component **attaches** its scoped `NodeEditorState` to the singleton `INodeEditorStateBridge`.

2. **MCP receives request**: When an MCP client sends a command (e.g., "add a node"), the request arrives at the HTTP endpoint. The MCP ability provider uses `BridgedNodeEditorState`, which delegates to whatever state is currently attached to the bridge.

3. **State is mutated**: The ability provider calls methods on `BridgedNodeEditorState` (e.g., `AddNode`), which forwards to the real `NodeEditorState` in the active circuit.

4. **UI updates**: Because `NodeEditorState` raises events, the Blazor components in the browser react and re-render—the user sees the node appear on their canvas in real-time.

5. **Browser closes**: When the browser tab closes, the circuit disposes and **detaches** from the bridge. The bridge's `Current` becomes null, and subsequent MCP calls return an error: "No active editor session."

### Thread Safety

The `NodeEditorStateBridge` uses thread-safe operations to handle concurrent access:
- Attach and Detach are atomic operations
- `BridgedNodeEditorState` checks for a valid reference before every call
- The `InvokeAsync` dispatcher marshals external calls safely onto Blazor's synchronization context

## Code Flow

### Attaching (Browser Opens)

```csharp
// In Home.razor or the main editor component
@inject NodeEditorState State
@inject INodeEditorStateBridge Bridge

protected override void OnInitialized()
{
    Bridge.Attach(State);
}

public void Dispose()
{
    Bridge.Detach();
}
```

### MCP Ability Using the Bridge

```csharp
// In an MCP Ability Provider
public class NodeAbilityProvider : IAbilityProvider
{
    private readonly BridgedNodeEditorState _state;

    public NodeAbilityProvider(BridgedNodeEditorState state)
    {
        _state = state;
    }

    public async Task<string> AddNode(string definitionId, double x, double y)
    {
        // This calls through the bridge to the active circuit's state
        var node = CreateNodeFromDefinition(definitionId);
        node.Position = new Point2D(x, y);
        _state.AddNode(node);  // → Bridge → Real NodeEditorState → UI updates
        return node.Data.Id;
    }
}
```

### Error When No Session

```csharp
// BridgedNodeEditorState checks for a valid bridge target
public void AddNode(NodeViewModel node)
{
    var state = _bridge.Current
        ?? throw new InvalidOperationException(
            "No active editor session. Open the Node Editor in the browser first.");
    state.AddNode(node);
}
```

## API Key Security

The MCP HTTP endpoint is protected by API key authentication:

1. **Key generation**: A 32-byte cryptographic random key is generated and stored at `%LocalAppData%/NodeEditor/mcp-api-key.dat`
2. **Middleware**: `McpApiKeyMiddleware` validates the `X-API-Key` header before requests reach the MCP endpoint
3. **Timing-safe**: Validation uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks
4. **Revocation**: Keys can be revoked and regenerated from the Settings panel

## Configuration

Enable MCP in `appsettings.json`:

```json
{
  "Mcp": {
    "Enabled": true,
    "RoutePattern": "/mcp"
  }
}
```

## Important Notes

- **One active session**: The bridge supports one active Blazor circuit at a time. If multiple browser tabs are open, the last one to initialize "wins."
- **Open browser first**: The browser must be open with the Node Editor loaded before MCP commands will work.
- **Real-time feedback**: Because the bridge forwards to the real state, changes appear on the canvas instantly—nodes appear, connections draw, execution runs.
- **HTTPS redirect**: The WebHost skips HTTPS redirect for the MCP route to avoid issues with SSE streaming.

## Namespaces

| Type | Namespace |
|------|-----------|
| `INodeEditorStateBridge` | `NodeEditor.Net.Services` |
| `NodeEditorStateBridge` | `NodeEditor.Net.Services` |
| `BridgedNodeEditorState` | `NodeEditor.Net.Services` |
| `McpApiKeyMiddleware` | `NodeEditor.Mcp` |
| `McpApiKeyService` | `NodeEditor.Net.Services` |
| `McpOptions` | `NodeEditor.Net.Services` |
