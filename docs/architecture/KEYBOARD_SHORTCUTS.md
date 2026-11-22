# Keyboard Shortcuts

| Context | Shortcut | Action |
|---|---|---|
| Anywhere | `Ctrl+Alt+H` (configurable) | Show or hide the Hora window |
| Wizard | `Enter` | Next or finish |
| Wizard | `Esc` | Close the wizard without completing setup |
| Wizard | `Tab` / `Shift+Tab` | Move focus between controls |
| Window | `Alt+F4` | Close, honoring the close-behavior setting |
| Window | `Ctrl+W` | Hide to tray |
| Window | `F5` | Refresh the visible page |
| Lists and grids | `Tab`, arrow keys | Move through controls; swatches are buttons |
| Language and theme combos | `Enter`, arrow keys | Open, preview, and select |

## Configuring the global shortcut

Settings, Behavior, Keyboard shortcut offers a validated preset list:

- `Ctrl+Alt+H` (default)
- `Ctrl+Alt+N`
- `Ctrl+Shift+H`
- `Alt+Shift+H`
- `Ctrl+Alt+F12`

`HotkeyCatalog.IsValid` enforces one to three unique modifiers, one key token
(`A` to `Z`, `0` to `9`, `F1` to `F24`), and a maximum length, so an invalid
gesture can never reach `RegisterHotKey`. The diagnostics page re-validates the
stored gesture and reports a failure instead of failing silently.

If Windows already owns the combination, registration fails; the settings page
reports that the shortcut could not be registered and keeps the previous one.
