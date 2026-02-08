# Overlays

Overlays are non-functional visual shapes that sit behind nodes on the canvas. They help you organize and annotate your graph by grouping related nodes into labeled regions.

## What They Are

An overlay is a colored rectangle with optional title and body text that renders behind nodes. Overlays don't affect execution or connections—they're purely organizational. Think of them as visual "folders" or "sections" that make large graphs easier to navigate.

## Why They Matter

Large graphs with dozens or hundreds of nodes can become difficult to understand at a glance. Overlays let you:
- **Group related nodes** into logical sections (e.g., "Input Processing", "AI Logic", "Output")
- **Add documentation** directly on the canvas via body text
- **Color-code** different subsystems for quick identification
- **Communicate intent** to other users viewing your graph

## Properties

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `string` | Unique identifier |
| `Title` | `string` | Header text displayed at the top of the overlay |
| `Body` | `string?` | Optional description text |
| `Position` | `Point2D` | Location on the canvas (top-left corner) |
| `Size` | `Size2D` | Width and height of the region |
| `Color` | `ColorValue` | Background color (RGB) |
| `Opacity` | `double` | Transparency level (0.0 = invisible, 1.0 = opaque) |

## Creating Overlays

### Through the UI

Use the **Organizer Overlay** tool in the canvas toolbar:
1. Click the organizer tool
2. Click and drag on the canvas to define the region
3. Enter a title and optional body text
4. Choose a color and opacity

### Programmatically

```csharp
using NodeEditor.Net.ViewModels;
using NodeEditor.Net.Models;

editorState.AddOverlay(new OverlayViewModel
{
    Id = Guid.NewGuid().ToString(),
    Title = "Input Processing",
    Body = "These nodes handle raw user input and normalization",
    Position = new Point2D(100, 100),
    Size = new Size2D(400, 300),
    Color = new ColorValue(50, 100, 200),
    Opacity = 0.15
});
```

## Selection

Overlays support selection, similar to nodes:
- Click on an overlay to select it
- Selected overlays can be moved, resized, or deleted
- The `OverlaySelectionChanged` event fires when overlay selection changes

```csharp
editorState.OverlaySelectionChanged += (sender, args) =>
{
    var selected = args.CurrentSelection;
    Console.WriteLine($"Selected {selected.Count} overlays");
};
```

## Serialization

Overlays are automatically serialized as part of the `GraphData` model:

```json
{
  "version": 1,
  "nodes": [...],
  "connections": [...],
  "overlays": [
    {
      "id": "overlay-1",
      "title": "Input Processing",
      "body": "These nodes handle raw user input",
      "x": 100,
      "y": 100,
      "width": 400,
      "height": 300,
      "color": { "r": 50, "g": 100, "b": 200 },
      "opacity": 0.15
    }
  ]
}
```

When a graph is saved and loaded, overlays are preserved along with all other graph data.

## MCP Integration

Overlays are fully accessible through MCP abilities:

| Ability | Description |
|---------|-------------|
| `overlay.add` | Create a new overlay on the canvas |
| `overlay.remove` | Remove an overlay by ID |
| `overlay.list` | List all overlays in the graph |
| `overlay.get` | Get details of a specific overlay |
| `overlay.select` | Select an overlay |
| `overlay.remove_selected` | Remove all selected overlays |

## Component

The `OrganizerOverlay.razor` component renders overlays on the canvas:
- Renders below nodes but above the canvas background
- Supports drag-to-move and drag-to-resize handles
- Displays title and body text
- Applies color and opacity styling

## Namespaces

| Type | Namespace |
|------|-----------|
| `OverlayViewModel` | `NodeEditor.Net.ViewModels` |
| `OverlayData` | `NodeEditor.Net.Models` |
| `ColorValue` | `NodeEditor.Net.Models` |
