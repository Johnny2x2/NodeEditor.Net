# Plugin Customization Guide

This document describes everything a plugin can currently customize in NodeEditorMax.

---

## 1. Custom Nodes

Plugins define node logic by creating classes that implement `INodeContext` and decorating methods with `[NodeAttribute]`. Each decorated method becomes a node in the editor.

### `[Node]` Attribute Properties

| Property | Type | Default | Description |
|---|---|---|---|
| `Name` | `string` | `"Node"` | Display name in the editor |
| `Menu` | `string` | `""` | Submenu path for the node picker |
| `Category` | `string` | `"General"` | Grouping category (e.g. `"Test"`, `"Test/Image"`) |
| `Description` | `string` | `"Some node."` | Tooltip / description text |
| `IsCallable` | `bool` | `true` | Whether the node has execution flow sockets |
| `IsExecutionInitiator` | `bool` | `false` | Whether the node can start an execution chain |

### Socket Discovery

Input and output sockets are derived automatically from method signatures:

- **Regular parameters** → input sockets
- **`out` parameters** → output sockets
- **`ExecutionPath` type** → execution flow sockets

### Example

```csharp
public sealed class MyContext : INodeContext
{
    [Node("Echo String", category: "Utilities", description: "Echoes a string", isCallable: false)]
    public void Echo(string Input, out string Output)
    {
        Output = Input;
    }

    [Node("Start", category: "Flow", isCallable: true, isExecutionInitiator: true)]
    public void Start(out ExecutionPath Exit)
    {
        Exit = new ExecutionPath();
        Exit.Signal();
    }
}
```

---

## 2. Node Registration

In the `Register(INodeRegistryService registry)` method, plugins register their node definitions with the editor. Two approaches:

- **Auto-discovery**: `registry.RegisterFromAssembly(typeof(MyPlugin).Assembly)` scans the assembly for all `[Node]`-attributed methods on `INodeContext` implementations.
- **Manual**: `registry.RegisterDefinitions(...)` for fine-grained control over which definitions are added.

The registry also supports removing definitions (`RemoveDefinitions`, `RemoveDefinitionsFromAssembly`) and querying via `GetCatalog()`.

---

## 3. Dependency Injection / Services

Plugins can register their own services into an `IServiceCollection` by overriding:

```csharp
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IMyService, MyService>();
}
```

These services are scoped per-plugin through the `IPluginServiceRegistry` and are available during `OnInitializeAsync` via the provided `IServiceProvider`.

---

## 4. Custom Socket Editors

Plugins can provide custom Blazor UI for editing socket values inline on nodes. This is done by implementing `INodeCustomEditor`:

```csharp
public interface INodeCustomEditor
{
    bool CanEdit(SocketData socket);
    RenderFragment Render(SocketEditorContext context);
}
```

- **`CanEdit`** — Inspects the socket's type, name, or `EditorHint` to decide if this editor should handle it.
- **`Render`** — Returns a Blazor `RenderFragment` that renders the custom editor UI.

Custom editors are **auto-discovered** from the plugin assembly by the `PluginLoader` — no manual registration needed. Simply include a class implementing `INodeCustomEditor` and a corresponding `.razor` component in your plugin project.

### Example

```csharp
public sealed class ImageEditorDefinition : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        if (socket.IsExecution || !socket.IsInput) return false;
        return socket.Name.Equals("ImagePath", StringComparison.Ordinal);
    }

    public RenderFragment Render(SocketEditorContext context)
    {
        return builder =>
        {
            builder.OpenComponent<ImageEditor>(0);
            builder.AddAttribute(1, "Context", context);
            builder.CloseComponent();
        };
    }
}
```

---

## 5. Custom Log Channels

Plugins that also implement `ILogChannelAware` can register named log channels:

```csharp
public sealed class MyPlugin : INodePlugin, ILogChannelAware
{
    // ... INodePlugin members ...

    public void RegisterChannels(ILogChannelRegistry registry)
    {
        registry.RegisterChannel("My Plugin Output", pluginId: Id);
    }
}
```

This is an **opt-in interface** — existing plugins that don't implement it are unaffected. Channels registered here are visible across the entire editor and support configurable `ChannelClearPolicy` values (`Manual` by default).

---

## 6. Event Subscriptions

Plugins can subscribe to and publish editor events through `IPluginEventBus` (obtained via DI during `OnInitializeAsync`):

| Event | Subscribe Method | Publish Method |
|---|---|---|
| Node added | `SubscribeNodeAdded` | `PublishNodeAdded` |
| Node removed | `SubscribeNodeRemoved` | `PublishNodeRemoved` |
| Connection added | `SubscribeConnectionAdded` | `PublishConnectionAdded` |
| Connection removed | `SubscribeConnectionRemoved` | `PublishConnectionRemoved` |
| Selection changed | `SubscribeSelectionChanged` | `PublishSelectionChanged` |
| Connection selection changed | `SubscribeConnectionSelectionChanged` | `PublishConnectionSelectionChanged` |
| Viewport changed | `SubscribeViewportChanged` | `PublishViewportChanged` |
| Zoom changed | `SubscribeZoomChanged` | `PublishZoomChanged` |
| Socket values changed | `SubscribeSocketValuesChanged` | `PublishSocketValuesChanged` |
| Node execution state changed | `SubscribeNodeExecutionStateChanged` | `PublishNodeExecutionStateChanged` |
| Log message | `SubscribeLogMessage` | `PublishLogMessage` |

All `Subscribe*` methods return `IDisposable` for clean unsubscription.

---

## 7. Lifecycle Hooks

The `INodePlugin` interface provides lifecycle callbacks that fire at different stages:

| Hook | Signature | When It Fires |
|---|---|---|
| `Register` | `void Register(INodeRegistryService)` | During plugin loading — register nodes here |
| `ConfigureServices` | `void ConfigureServices(IServiceCollection)` | After registration — add DI services |
| `OnLoadAsync` | `Task OnLoadAsync(CancellationToken)` | After assembly is loaded |
| `OnInitializeAsync` | `Task OnInitializeAsync(IServiceProvider, CancellationToken)` | After DI container is built; full `IServiceProvider` available |
| `OnUnloadAsync` | `Task OnUnloadAsync(CancellationToken)` | Before plugin is unloaded |
| `OnError` | `void OnError(Exception)` | On unhandled plugin exception |
| `Unload` | `void Unload()` | Synchronous final cleanup |

All async hooks have default no-op implementations, so plugins only need to override what they use.

---

## 8. Plugin Manifest (`plugin.json`)

Each plugin ships a `plugin.json` file alongside its DLLs:

```json
{
    "Id": "com.nodeeditormax.myplugin",
    "Name": "My Plugin",
    "Version": "1.0.0",
    "MinApiVersion": "1.0.0",
    "EntryAssembly": "MyPlugin.dll",
    "Category": "Utilities"
}
```

| Field | Required | Description |
|---|---|---|
| `Id` | Yes | Unique plugin identifier (reverse-domain convention) |
| `Name` | Yes | Human-readable display name |
| `Version` | Yes | Plugin version (semver) |
| `MinApiVersion` | Yes | Minimum host API version required |
| `EntryAssembly` | No | DLL filename containing the `INodePlugin` implementation |
| `Category` | No | Plugin category for marketplace/organization |

---

## What Plugins Cannot Currently Customize

The following areas are **not** exposed to the plugin system:

- Canvas themes or visual styling
- Toolbar items or menus
- Context menu entries
- Connection rendering rules or styles
- Keyboard shortcuts
- Panel/dock layout
