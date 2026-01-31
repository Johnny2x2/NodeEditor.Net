# Stage 08 — Styling & Theming

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
- Visual quality matches WinForms version.
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

## Checklist
- [ ] Theme applies without reloading components
- [ ] High-contrast theme meets accessibility ratios
- [ ] Socket colors match data types
