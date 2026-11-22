# Window and Viewport Matrix

The shell is a single resizable window with three columns: a 74 px icon rail, a
238 px contextual rail, and the page host. The layout is verified at these sizes.

| Width x Height | Behavior |
|---|---|
| 940 x 640 (minimum) | Rails fixed, page host scrolls vertically, no horizontal scrollbar |
| 1180 x 780 (default) | Two-column card rows, donut and legend side by side |
| 1440 x 900 | Wider cards, same content order, no stretched text |
| 1920 x 1080 | Content capped by card padding, not by text width |

## Resize rules

- `MinWidth` 940 and `MinHeight` 640 are enforced by `SettingsStore.Sanitize`, so
  a corrupted settings file cannot restore an unusable window.
- Geometry is saved on every move and resize and clamped on load, so a window
  that was on a disconnected monitor reopens on the primary screen.
- `ResizeBorderThickness` is 6 px in normal mode and 0 in Mini mode; Mini mode
  keeps the same window but reduces the visible chrome.
- The page host is the only scrolling region. Rails and the status bar never
  scroll, so navigation and status stay reachable.

## Typography

| Token | Size | Use |
|---|---|---|
| `FontMicro` | 11 | Captions, meta, badges |
| `FontSmall` | 12 | Secondary rows |
| `FontBody` | 13 | Default text |
| `FontHeading` | 15 | Section headings |
| `FontDisplay` | 30 | Dashboard total |

Text scale multiplies these values at runtime; layout reflows rather than clips.

## DPI

Checked at 100%, 125%, 150%, and 200% scaling. Because the process is per-monitor
V2 aware, moving the window between a 100% and a 200% monitor re-renders at the
new scale without a restart.
