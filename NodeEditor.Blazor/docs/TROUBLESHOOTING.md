# Troubleshooting Guide

This guide covers common issues you may encounter when using NodeEditor.Blazor and provides step-by-step solutions.

## Table of Contents

1. [Nodes Don't Appear in Context Menu](#nodes-dont-appear-in-context-menu)
2. [Connections Don't Validate Types](#connections-dont-validate-types)
3. [Events Fire But UI Doesn't Update](#events-fire-but-ui-doesnt-update)
4. [Plugin Loading Fails on iOS](#plugin-loading-fails-on-ios)
5. [Serialization Version Mismatch](#serialization-version-mismatch)
6. [Performance Issues with Large Graphs](#performance-issues-with-large-graphs)
7. [Nodes Render Outside Viewport](#nodes-render-outside-viewport)
8. [Custom Socket Editors Not Appearing](#custom-socket-editors-not-appearing)
9. [JavaScript Interop Errors](#javascript-interop-errors)
10. [Zoom/Pan Not Working Properly](#zoompan-not-working-properly)

---

## Nodes Don't Appear in Context Menu

### Symptoms
- Right-clicking on canvas shows empty or incomplete context menu
- Specific node types are missing from the menu
- Context menu doesn't appear at all

### Common Causes
1. Node assembly not registered with `INodeRegistryService`
2. Node class does not extend `NodeBase`
3. `Configure(INodeBuilder)` not implemented or has errors

### Solution

**Step 1:** Verify node assembly registration in `Program.cs`:

```csharp
// After building services
var registry = app.Services.GetRequiredService<INodeRegistryService>();
registry.RegisterFromAssembly(typeof(MyNode).Assembly);
```

**Step 2:** Check that your node extends `NodeBase` and overrides `Configure`:

```csharp
public class ProcessDataNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Process Data")
               .Category("Custom")
               .Input<int>("Input", 0)
               .Output<int>("Output")
               .Callable();
    }

    public override async Task ExecuteAsync(
        INodeExecutionContext context, CancellationToken ct)
    {
        var input = context.GetInput<int>("Input");
        context.SetOutput("Output", input * 2);
        await context.TriggerAsync("Exit");
    }
}
```

**Step 3:** Ensure the class is `public` and has a parameterless constructor.

**Step 4:** Check browser console for registration errors:

```javascript
// Open browser DevTools (F12) and look for errors like:
// "Failed to register node from assembly"
```

### Verification
After making changes, refresh the page and verify nodes appear in the context menu at the correct category.

---

## Connections Don't Validate Types

### Symptoms
- Can connect incompatible socket types
- Valid connections are rejected
- Type validation errors in console

### Common Causes
1. SocketTypeResolver not configured correctly
2. Custom type mappings missing
3. Socket type names don't match

### Solution

**Step 1:** Configure SocketTypeResolver in services:

```csharp
builder.Services.AddNodeEditor(config =>
{
    config.ConfigureSocketTypeResolver(resolver =>
    {
        // Map C# types to socket type names
        resolver.RegisterType<int>("Number");
        resolver.RegisterType<string>("Text");
        resolver.RegisterType<bool>("Boolean");
        resolver.RegisterType<MyCustomType>("Custom");
    });
});
```

**Step 2:** Verify socket type names are consistent between nodes:

```csharp
// This will work - both use int
public class AddNode : NodeBase
{
    public override void Configure(INodeBuilder builder)
    {
        builder.Name("Add")
               .Input<int>("A", 0)
               .Input<int>("B", 0)
               .Output<int>("Result");
    }
}

// Connections are validated by the C# type system via the builder.
// SocketTypeResolver checks type assignability automatically.
```

**Step 3:** For generic compatibility, use the resolver's compatibility rules:

```csharp
config.ConfigureSocketTypeResolver(resolver =>
{
    // Allow "Any" to connect to anything
    resolver.RegisterCompatibilityRule("Any", "*");
    
    // Allow Number to connect to Float
    resolver.RegisterCompatibilityRule("Number", "Float");
});
```

**Step 4:** Check validation errors in State events:

```csharp
State.OnValidationError += (sender, error) =>
{
    Console.WriteLine($"Validation error: {error.Message}");
};
```

### Verification
Try connecting sockets - valid connections should succeed, invalid ones should be rejected with visual feedback.

---

## Events Fire But UI Doesn't Update

### Symptoms
- State changes but components don't re-render
- Node positions update in code but not visually
- Connections added/removed but not visible

### Common Causes
1. Not calling StateHasChanged() in event handlers
2. Event handlers on wrong thread (not UI thread)
3. Using immutable state incorrectly
4. Component not subscribed to state events

### Solution

**Step 1:** Ensure StateHasChanged() is called in event handlers:

```csharp
protected override void OnInitialized()
{
    State.OnNodeAdded += async (sender, node) =>
    {
        // Process the event
        await DoSomethingAsync(node);
        
        // Force UI update
        await InvokeAsync(StateHasChanged);
    };
}
```

**Step 2:** Use InvokeAsync for thread-safe updates:

```csharp
// ❌ BAD - might not be on UI thread
State.OnNodeMoved += (sender, node) =>
{
    StateHasChanged(); // May throw exception
};

// ✅ GOOD - guaranteed to run on UI thread
State.OnNodeMoved += async (sender, node) =>
{
    await InvokeAsync(StateHasChanged);
};
```

**Step 3:** Verify you're modifying state through NodeEditorState methods, not directly:

```csharp
// ❌ BAD - direct modification doesn't trigger events
var node = State.Nodes.First();
node.Position = new Point2D(100, 100);

// ✅ GOOD - use state methods
await State.MoveNodeAsync(nodeId, new Point2D(100, 100));
```

**Step 4:** Check that components are properly subscribed:

```csharp
@implements IDisposable

@code {
    protected override void OnInitialized()
    {
        State.OnNodeAdded += HandleNodeAdded;
    }
    
    private async void HandleNodeAdded(object sender, NodeViewModel node)
    {
        await InvokeAsync(StateHasChanged);
    }
    
    public void Dispose()
    {
        State.OnNodeAdded -= HandleNodeAdded;
    }
}
```

**Step 5:** For complex updates, use explicit rendering:

```csharp
@code {
    private bool _shouldRender = true;
    
    protected override bool ShouldRender()
    {
        return _shouldRender;
    }
    
    private async void HandleStateChange()
    {
        _shouldRender = true;
        await InvokeAsync(StateHasChanged);
    }
}
```

### Verification
Monitor browser console for exceptions and verify UI updates when state changes.

---

## Plugin Loading Fails on iOS

### Symptoms
- Plugins load on Windows/Android but not iOS
- "Assembly not found" errors
- Plugin methods not registered

### Common Causes
1. iOS AOT compilation doesn't support dynamic assembly loading
2. Plugin not included in app bundle
3. Incorrect plugin path

### Solution

**Step 1:** For iOS, use compile-time plugin registration instead of runtime loading:

```csharp
// ❌ BAD - won't work on iOS
builder.Services.AddNodeEditor(config =>
{
    config.LoadPluginsFrom("plugins/");
});

// ✅ GOOD - works on all platforms including iOS
builder.Services.AddNodeEditor(config =>
{
    config.RegisterPlugin<MyPlugin>();
});
```

**Step 2:** If you must use dynamic loading, ensure assemblies are embedded:

```xml
<!-- In .csproj -->
<ItemGroup>
    <EmbeddedResource Include="Plugins\MyPlugin.dll" />
</ItemGroup>
```

**Step 3:** Use conditional compilation for platform-specific loading:

```csharp
#if IOS || MACCATALYST
    // Compile-time registration for iOS
    config.RegisterPlugin<MyPlugin>();
#else
    // Dynamic loading for other platforms
    config.LoadPluginsFrom("plugins/");
#endif
```

**Step 4:** Verify plugin is included in bundle:

```xml
<ItemGroup Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">
    <BundleResource Include="Plugins\**\*.*" />
</ItemGroup>
```

**Step 5:** Add LinkerConfig.xml to preserve plugin types:

```xml
<?xml version="1.0" encoding="utf-8" ?>
<linker>
    <assembly fullname="MyPlugin">
        <type fullname="MyPlugin.MyPluginClass" preserve="all" />
    </assembly>
</linker>
```

### Verification
Deploy to iOS device or simulator and check console for plugin registration messages.

---

## Serialization Version Mismatch

### Symptoms
- "Cannot deserialize graph" errors
- Loaded graphs missing nodes/connections
- Version number warnings

### Common Causes
1. Graph saved with newer version of library
2. Breaking changes in data model
3. Custom serialization logic incompatible

### Solution

**Step 1:** Check version numbers in serialized data:

```csharp
var serializer = serviceProvider.GetRequiredService<IGraphSerializer>();

try
{
    var graph = await serializer.DeserializeAsync(json);
}
catch (SerializationException ex)
{
    Console.WriteLine($"Version mismatch: {ex.Message}");
    // Handle version mismatch
}
```

**Step 2:** Implement version migration:

```csharp
public class GraphMigrator
{
    public string MigrateFromV1ToV2(string v1Json)
    {
        var doc = JsonDocument.Parse(v1Json);
        var root = doc.RootElement;
        
        // Transform old format to new format
        var migrated = new
        {
            Version = "2.0",
            Nodes = root.GetProperty("nodes").EnumerateArray()
                .Select(n => new
                {
                    Id = n.GetProperty("id").GetString(),
                    Type = n.GetProperty("type").GetString(),
                    // Add new required properties
                    Category = "Default",
                    Position = n.GetProperty("position")
                })
                .ToArray(),
            Connections = root.GetProperty("connections")
        };
        
        return JsonSerializer.Serialize(migrated);
    }
}
```

**Step 3:** Add version checking to your load logic:

```csharp
private async Task<GraphData> LoadGraphWithMigration(string json)
{
    var doc = JsonDocument.Parse(json);
    var version = doc.RootElement.GetProperty("Version").GetString();
    
    switch (version)
    {
        case "1.0":
            json = MigrateFromV1ToV2(json);
            goto case "2.0";
            
        case "2.0":
            return await _serializer.DeserializeAsync(json);
            
        default:
            throw new NotSupportedException($"Version {version} not supported");
    }
}
```

**Step 4:** Always include version in saved graphs:

```csharp
public class GraphData
{
    public string Version { get; set; } = "2.0";
    public List<NodeViewModel> Nodes { get; set; }
    public List<ConnectionData> Connections { get; set; }
}
```

**Step 5:** For incompatible versions, provide clear error messages:

```csharp
if (loadedVersion > currentVersion)
{
    await DisplayAlert(
        "Version Mismatch",
        $"This graph was created with a newer version ({loadedVersion}) " +
        $"of NodeEditor. Please update the app to version {loadedVersion} or higher.",
        "OK");
}
```

### Verification
Test loading graphs from previous versions and verify all data is preserved or properly migrated.

---

## Performance Issues with Large Graphs

### Symptoms
- Slow rendering with 100+ nodes
- Laggy pan/zoom
- High CPU usage when idle
- Frame rate drops below 30 FPS

### Common Causes
1. Viewport culling not enabled
2. Too many unnecessary re-renders
3. Complex node templates
4. No render optimization

### Solution

**Step 1:** Ensure ViewportCuller is registered and enabled:

```csharp
builder.Services.AddNodeEditor(config =>
{
    config.EnableViewportCulling = true;
});
```

**Step 2:** Verify ShouldRender optimization in custom components:

```csharp
// In your custom node component
public partial class MyNodeComponent : ComponentBase
{
    private Point2D _lastPosition;
    private string _lastTitle;
    
    protected override bool ShouldRender()
    {
        var shouldRender = 
            Node.Position != _lastPosition ||
            Node.Title != _lastTitle;
            
        if (shouldRender)
        {
            _lastPosition = Node.Position;
            _lastTitle = Node.Title;
        }
        
        return shouldRender;
    }
}
```

**Step 3:** Use virtualization for large node lists:

```razor
<!-- Use Virtualize for large collections -->
<Virtualize Items="@State.Nodes" Context="node">
    <NodeComponent Node="@node" />
</Virtualize>
```

**Step 4:** Optimize connection rendering:

```csharp
// Only render visible connections
@foreach (var connection in _visibleConnections)
{
    <ConnectionPath Connection="@connection" />
}

@code {
    private List<ConnectionData> _visibleConnections;
    
    protected override void OnParametersSet()
    {
        _visibleConnections = ViewportCuller.GetVisibleConnections(
            State.Connections,
            State.Nodes);
    }
}
```

**Step 5:** Profile with browser DevTools:

```
1. Open Chrome DevTools (F12)
2. Go to Performance tab
3. Click Record
4. Interact with node editor
5. Stop recording
6. Look for long frames (yellow/red bars)
7. Identify bottleneck functions
```

**Step 6:** Reduce update frequency:

```csharp
private System.Timers.Timer _updateThrottle;

protected override void OnInitialized()
{
    _updateThrottle = new System.Timers.Timer(16); // ~60 FPS
    _updateThrottle.Elapsed += async (s, e) =>
    {
        if (_needsUpdate)
        {
            _needsUpdate = false;
            await InvokeAsync(StateHasChanged);
        }
    };
    _updateThrottle.Start();
}
```

### Verification
- Test with 500+ nodes
- Monitor FPS in browser DevTools
- Target: 60 FPS for pan/zoom, 30+ FPS for complex operations

---

## Nodes Render Outside Viewport

### Symptoms
- Nodes visible when they should be culled
- Blank spaces where nodes should appear
- Incorrect culling calculations

### Common Causes
1. Viewport bounds not updated
2. Node bounds calculation incorrect
3. CoordinateConverter not initialized

### Solution

**Step 1:** Verify viewport tracking is working:

```csharp
// In NodeEditorCanvas.razor.cs
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
            "import", "./_content/NodeEditor.Blazor/nodeEditorCanvas.js");
            
        await _jsModule.InvokeVoidAsync(
            "observeCanvasSize",
            _canvasElement,
            DotNetObjectReference.Create(this));
            
        Console.WriteLine("Viewport tracking initialized");
    }
}
```

**Step 2:** Add logging to culling calculations:

```csharp
public List<NodeViewModel> GetVisibleNodes(
    IEnumerable<NodeViewModel> allNodes,
    bool alwaysIncludeSelected = true)
{
    var viewport = _coordinateConverter.GetViewportBounds();
    Console.WriteLine($"Viewport: {viewport}");
    
    var visible = allNodes.Where(node =>
    {
        var nodeBounds = CalculateNodeBounds(node);
        var isVisible = viewport.Intersects(nodeBounds);
        Console.WriteLine($"Node {node.Id}: {isVisible}");
        return isVisible;
    }).ToList();
    
    return visible;
}
```

**Step 3:** Verify node bounds include padding:

```csharp
private Rect2D CalculateNodeBounds(NodeViewModel node)
{
    const double padding = 20; // Extra space around node
    
    return new Rect2D(
        node.Position.X - padding,
        node.Position.Y - padding,
        node.Width + (padding * 2),
        node.Height + (padding * 2));
}
```

**Step 4:** Force update when viewport changes:

```csharp
[JSInvokable]
public async Task OnCanvasResize(double width, double height)
{
    _canvasSize = new Size2D(width, height);
    
    // Force culling recalculation
    UpdateCulling();
    
    await InvokeAsync(StateHasChanged);
}
```

**Step 5:** Disable culling temporarily to verify it's the issue:

```csharp
// In NodeEditorCanvas.razor
@if (_debugNoCulling)
{
    @foreach (var node in State.Nodes)
    {
        <NodeComponent Node="@node" />
    }
}
else
{
    @foreach (var node in _visibleNodes)
    {
        <NodeComponent Node="@node" />
    }
}
```

### Verification
Pan around the canvas and verify nodes appear/disappear smoothly at viewport edges.

---

## Custom Socket Editors Not Appearing

### Symptoms
- Default input used instead of custom editor
- Editor property returns null
- Custom editor not registered

### Common Causes
1. INodeCustomEditor not implemented correctly
2. Editor not registered in DI container
3. Socket type mismatch
4. Editor component has compile errors

### Solution

**Step 1:** Implement INodeCustomEditor interface:

```csharp
public class ColorPickerEditor : INodeCustomEditor
{
    public bool CanEdit(SocketData socket)
    {
        return socket.TypeName == typeof(Color).FullName
               && socket.IsInput
               && !socket.IsExecution;
    }

    public RenderFragment Render(SocketEditorContext context)
        => builder =>
        {
            builder.OpenComponent<ColorPickerComponent>(0);
            builder.AddAttribute(1, nameof(ColorPickerComponent.Context), context);
            builder.CloseComponent();
        };
}
```

**Step 2:** Register editor in services:

```csharp
services.AddSingleton<INodeCustomEditor, ColorPickerEditor>();
```

**Step 3:** Create editor component:

```razor
@* ColorPickerComponent.razor *@
@inherits NodeCustomEditorBase

<input type="color" 
       value="@ColorValue" 
       @onchange="OnColorChanged" />

@code {
    private string ColorValue => Value?.ToString() ?? "#000000";
    
    private async Task OnColorChanged(ChangeEventArgs e)
    {
        await ValueChanged.InvokeAsync(e.Value?.ToString());
    }
}
```

**Step 4:** Verify EditorRegistry can find your editor:

```csharp
@inject EditorRegistry EditorRegistry

@code {
    protected override void OnInitialized()
    {
        var editor = EditorRegistry.GetEditor(typeof(Color), "Color");
        if (editor == null)
        {
            Console.WriteLine("Editor not found for Color type!");
        }
        else
        {
            Console.WriteLine($"Found editor: {editor.GetType().Name}");
        }
    }
}
```

**Step 5:** Check for component errors:

```
1. Build the project
2. Look for compilation errors in ColorPickerComponent
3. Verify namespace is correct
4. Ensure component inherits NodeCustomEditorBase
```

### Verification
Create a node with a Color socket and verify the color picker appears instead of default input.

---

## JavaScript Interop Errors

### Symptoms
- "Cannot read property of undefined" in console
- JS module fails to load
- JSInvokable methods not found

### Common Causes
1. JS file not included in wwwroot
2. Module path incorrect
3. .NET object reference disposed
4. Timing issues (calling JS before ready)

### Solution

**Step 1:** Verify JS file exists in wwwroot:

```
NodeEditor.Blazor/
  wwwroot/
    nodeEditorCanvas.js  ← Must be here
```

**Step 2:** Use correct import path with _content:

```csharp
// ✅ CORRECT - includes _content prefix
_jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
    "import", "./_content/NodeEditor.Blazor/nodeEditorCanvas.js");

// ❌ WRONG - missing _content
_jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
    "import", "./nodeEditorCanvas.js");
```

**Step 3:** Only call JS after firstRender:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && _jsModule != null)
    {
        try
        {
            await _jsModule.InvokeVoidAsync("initialize");
        }
        catch (JSException ex)
        {
            Console.WriteLine($"JS Error: {ex.Message}");
        }
    }
}
```

**Step 4:** Keep DotNetObjectReference alive:

```csharp
private DotNetObjectReference<NodeEditorCanvas>? _dotNetRef;

protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender)
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        await _jsModule.InvokeVoidAsync("setup", _dotNetRef);
    }
}

public async ValueTask DisposeAsync()
{
    _dotNetRef?.Dispose();
    if (_jsModule != null)
    {
        await _jsModule.DisposeAsync();
    }
}
```

**Step 5:** Add error handling in JS:

```javascript
export function observeCanvasSize(canvas, dotNetRef) {
    try {
        if (!canvas) {
            console.error('Canvas element is null');
            return;
        }
        
        const observer = new ResizeObserver(entries => {
            for (const entry of entries) {
                const { width, height } = entry.contentRect;
                dotNetRef.invokeMethodAsync('OnCanvasResize', width, height)
                    .catch(err => console.error('Failed to invoke .NET method:', err));
            }
        });
        
        observer.observe(canvas);
        observers.set(canvas, observer);
    } catch (error) {
        console.error('Error setting up resize observer:', error);
    }
}
```

### Verification
Check browser console for errors and verify JS methods execute successfully.

---

## Zoom/Pan Not Working Properly

### Symptoms
- Mouse wheel doesn't zoom
- Drag doesn't pan
- Zoom centers on wrong point
- Pan jumps or stutters

### Common Causes
1. Event handlers not registered
2. CoordinateConverter not updated
3. Transform not applied to canvas
4. Event propagation blocked

### Solution

**Step 1:** Verify mouse event handlers are registered:

```razor
<div class="node-editor-canvas"
     @onwheel="OnMouseWheel"
     @onmousedown="OnMouseDown"
     @onmousemove="OnMouseMove"
     @onmouseup="OnMouseUp"
     @onwheel:preventDefault="true">
    <!-- Canvas content -->
</div>
```

**Step 2:** Update CoordinateConverter state:

```csharp
private async Task OnMouseWheel(WheelEventArgs e)
{
    var delta = e.DeltaY > 0 ? 0.9 : 1.1;
    var mousePos = new Point2D(e.OffsetX, e.OffsetY);
    
    // Update zoom
    await State.SetZoomAsync(State.Zoom * delta, mousePos);
    
    // Update coordinate converter
    _coordinateConverter.SetZoom(State.Zoom);
    _coordinateConverter.SetPan(State.PanX, State.PanY);
    
    StateHasChanged();
}
```

**Step 3:** Apply transform to canvas group:

```razor
<svg width="100%" height="100%">
    <g transform="translate(@State.PanX, @State.PanY) scale(@State.Zoom)">
        @foreach (var node in _visibleNodes)
        {
            <NodeComponent Node="@node" />
        }
    </g>
</svg>
```

**Step 4:** Implement smooth panning:

```csharp
private Point2D _panStart;
private Point2D _panOffset;
private bool _isPanning;

private void OnMouseDown(MouseEventArgs e)
{
    if (e.Button == 1) // Middle mouse button
    {
        _isPanning = true;
        _panStart = new Point2D(e.ClientX, e.ClientY);
        _panOffset = new Point2D(State.PanX, State.PanY);
    }
}

private async Task OnMouseMove(MouseEventArgs e)
{
    if (_isPanning)
    {
        var delta = new Point2D(
            e.ClientX - _panStart.X,
            e.ClientY - _panStart.Y);
            
        await State.SetPanAsync(
            _panOffset.X + delta.X,
            _panOffset.Y + delta.Y);
            
        StateHasChanged();
    }
}

private void OnMouseUp(MouseEventArgs e)
{
    _isPanning = false;
}
```

**Step 5:** Clamp zoom to reasonable range:

```csharp
private async Task OnMouseWheel(WheelEventArgs e)
{
    var delta = e.DeltaY > 0 ? 0.9 : 1.1;
    var newZoom = Math.Clamp(State.Zoom * delta, 0.1, 5.0);
    
    var mousePos = new Point2D(e.OffsetX, e.OffsetY);
    await State.SetZoomAsync(newZoom, mousePos);
}
```

### Verification
Test zoom with mouse wheel and pan with middle mouse drag. Movement should be smooth and predictable.

---

## Additional Resources

### Performance Profiling
- Chrome DevTools Performance tab
- Blazor Server/WASM performance guidelines
- [Performance Best Practices](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance)

### Debugging Tools
- Browser Console (F12)
- Blazor debugging in VS Code
- Remote debugging for mobile devices

### Community Support
- GitHub Issues: [Report bugs and request features]
- Stack Overflow: Tag with `blazor` and `node-editor`
- Discord/Slack: [Community channels if available]

### Related Documentation
- [API Reference](API.md)
- [Migration Guide](MIGRATION.md)
- [Custom Nodes Tutorial](CUSTOM-NODES.md)
- Main README

---

## Getting Help

If you encounter issues not covered in this guide:

1. **Check the Examples**: Review the sample projects for working implementations
2. **Search Issues**: Look for similar problems in the GitHub issues
3. **Enable Logging**: Add console logging to identify the problem area
4. **Create Minimal Repro**: Isolate the issue in a minimal example
5. **Report Issue**: Create a GitHub issue with:
   - Description of the problem
   - Steps to reproduce
   - Expected vs actual behavior
   - Environment details (OS, browser, .NET version)
   - Relevant code snippets

## Version-Specific Issues

### v2.0
- Breaking change: Nodes now use `NodeBase` subclass pattern instead of `INodeContext` + `[Node]` attributes
- Migration: Rewrite node methods as `NodeBase` subclasses with `Configure(INodeBuilder)` and `ExecuteAsync`

### v1.5
- Known issue: iOS plugin loading requires compile-time registration
- Workaround: See [Plugin Loading Fails on iOS](#plugin-loading-fails-on-ios)

### v1.0
- Known issue: Large graphs (>500 nodes) may have performance issues
- Workaround: Upgrade to v1.5+ with viewport culling

---

*Last updated: February 2026*
