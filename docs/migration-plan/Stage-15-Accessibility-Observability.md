# Stage 15 — Accessibility, Observability & Diagnostics

## Goal
Ensure the editor is accessible and observable in production scenarios.

## Requirements
- Keyboard navigation supports selection, move, connect, and context menu actions.
- Focus states are visible and follow logical tab order (canvas and context menu are focusable).
- ARIA labels and roles are applied to interactive elements (nodes, sockets, canvas).
- Contrast ratios meet WCAG AA for default and high-contrast themes.
- Reduced motion setting is respected for animations.
- Structured logging for execution, plugin loading, and serialization errors.
- Diagnostics panel is available in dev builds and toggleable at runtime.
- Telemetry is optional, disabled by default, and documented if enabled.

## Deliverables
- Accessibility audit
- Diagnostics/telemetry hooks (optional)
- Error reporting and logging policy

## Tasks
1. Add ARIA labels and keyboard navigation support.
2. Validate contrast ratios for themes.
3. Add structured logs for execution, errors, and plugin loading.
4. Provide a diagnostics panel for development builds (optional).

## Acceptance Criteria
- Keyboard-only navigation is possible for key flows.
- Themes pass contrast checks.
- Logs provide actionable diagnostics.

### Testing Parameters
- NUnit/xUnit accessibility tests for focus navigation.
- NUnit/xUnit log capture tests for execution failures.

## Detailed Notes
- Use platform-appropriate logging in MAUI.
- Keep telemetry optional and disabled by default.

## Checklist
- [ ] ARIA labels added for editor UI
- [ ] High-contrast theme validated
- [ ] Diagnostics visible in dev builds

### What's Done
- ✅ Canvas and context menu are focusable (`tabindex` present)

### What's Remaining
- ❌ ARIA labels and roles for nodes, sockets, and canvas
- ❌ Keyboard navigation for select/move/connect
- ❌ Focus states and tab order review
- ❌ High-contrast theme validation
- ❌ Reduced-motion support
- ❌ Structured logging for execution, plugins, serialization
- ❌ Dev diagnostics panel

### What Should Be Done Next
1. Add ARIA roles/labels to node, socket, and canvas components.
2. Implement keyboard navigation and shortcuts for selection and movement.
3. Audit contrast and add a high-contrast theme option.
4. Respect reduced-motion preference for animations.
5. Add structured logging for execution and plugin load failures.
