# Stage 08 — Styling & Theming

## Status: 🟡 Partially Complete

### What's Done
- ✅ Base CSS in `wwwroot/css/node-editor.css`
- ✅ Dark theme implemented as default
- ✅ Responsive design for mobile
- ✅ Type-based socket colors (in `SocketComponent.razor`)
- ✅ Execution socket styling (diamond shape, white)
- ✅ Selection highlight styling

### What's Remaining
- ❌ `NodeEditorTheme` model for theme values
- ❌ CSS variables for theme swapping
- ❌ Theme switching at runtime
- ❌ High-contrast accessibility theme
- ❌ `SocketColorRegistry` service for type-to-color mapping

## Goal
Match or improve the visual style using CSS and theming models.

## Deliverables
- NodeEditorTheme model
- CSS for canvas, nodes, sockets, connections

## Tasks
1. Implement base CSS in wwwroot/css.
2. Add theme model and apply values via CSS variables.
3. Add type-based colors for sockets/connections.

## Acceptance Criteria
- Theme can be swapped at runtime.

### Testing Parameters
- NUnit/xUnit visual regression checks for default and high-contrast themes.
- NUnit/xUnit theme swap test completes without layout reflow errors.

## Dependencies
Stage 03.

## Risks / Notes
- Ensure accessibility contrast for key elements.

## Architecture Notes
Define a theme model with **CSS variables** so UI can swap themes without re-rendering components.
The theme should include:
- Node background, header, border
- Socket colors by data type
- Connection colors by type
- Canvas background and grid

## Detailed Tasks (Expanded)
1. **Theme model**
	- Create `NodeEditorTheme` with strongly typed values.
2. **CSS variables**
	- Apply theme values to `:root` or a scoped wrapper.
3. **Type colors**
	- Map known data types to colors (int, float, string, bool, execution).
4. **High contrast theme**
	- Provide an accessible alternative.

## Code Examples

### Theme model
```csharp
public sealed record class NodeEditorTheme(
	 ColorValue CanvasBackground,
	 ColorValue NodeBackground,
	 ColorValue NodeHeader,
	 ColorValue NodeBorder,
	 ColorValue ConnectionDefault);
```

### CSS variables
```css
.ne-root {
  --ne-canvas-bg: #1e1e1e;
  --ne-node-bg: #2d2d30;
  --ne-node-header: #3c3c3c;
  --ne-node-border: #4a4a4a;
  --ne-conn-default: #d0d0d0;
}

.ne-canvas { background: var(--ne-canvas-bg); }
.ne-node { background: var(--ne-node-bg); border-color: var(--ne-node-border); }
.ne-node-header { background: var(--ne-node-header); }
```

## Missing Architecture Gaps (to close in this stage)
- **Theme switcher**: apply different theme objects at runtime
- **Type color registry**: map type name → CSS variable
- **Grid rendering**: optional canvas/SVG background grid

## Implementation Notes (for next developer)

### Current CSS Location
All styles are in `NodeEditor.Blazor/wwwroot/css/node-editor.css`. This file includes:
- Canvas container and viewport
- Node styling (background, header, selection)
- Socket styling with type-based colors (hardcoded in component)
- Connection path styling
- Responsive breakpoints

### Converting to CSS Variables
Refactor existing hardcoded colors to use CSS variables:
```css
.ne-root {
  --ne-canvas-bg: #1e1e2e;
  --ne-canvas-grid: rgba(255, 255, 255, 0.03);
  --ne-node-bg: #2d2d3d;
  --ne-node-header: #3d3d5c;
  --ne-node-border: #3d3d5c;
  --ne-node-selected: #6699ff;
  --ne-socket-int: rgb(0, 200, 100);
  --ne-socket-float: rgb(100, 200, 255);
  --ne-socket-string: rgb(255, 150, 200);
  --ne-socket-bool: rgb(200, 50, 50);
  --ne-socket-exec: #ffffff;
}
```

### SocketColorRegistry Service
Move socket coloring from `SocketComponent.razor` to a service:
```csharp
public sealed class SocketColorRegistry
{
    private readonly Dictionary<string, ColorValue> _colors = new()
    {
        ["int"] = new(0, 200, 100),
        ["float"] = new(100, 200, 255),
        ["string"] = new(255, 150, 200),
        ["bool"] = new(200, 50, 50),
        ["execution"] = new(255, 255, 255),
    };
    
    public ColorValue GetColor(string typeName) =>
        _colors.GetValueOrDefault(typeName.ToLowerInvariant(), new(180, 180, 100));
}
```

### Theme Switching
Apply themes by setting a CSS class on the root element:
```razor
<div class="ne-root @ThemeClass">
    <NodeEditorCanvas ... />
</div>

@code {
    private string ThemeClass => Theme switch
    {
        NodeEditorTheme.Dark => "ne-theme-dark",
        NodeEditorTheme.Light => "ne-theme-light",
        NodeEditorTheme.HighContrast => "ne-theme-hc",
        _ => ""
    };
}
```

## Checklist
- [x] Base theme applies without reloading components
- [ ] High-contrast theme meets accessibility ratios (4.5:1 minimum)
- [x] Socket colors match data types
- [ ] Theme can be swapped at runtime
- [ ] CSS variables used for all theme colors
- [ ] Connection colors match socket types
