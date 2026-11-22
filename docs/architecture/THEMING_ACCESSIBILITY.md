# Theming and Accessibility

## Palettes

`ThemeCatalog` holds 28 palettes plus the `custom` slot. A palette is an id, an
accent, and a soft accent. Examples: `aurora`, `midnight`, `ocean`, `violet`,
`crimson`, `indigo`, `lime`, `mint`, `orchid`, `graphite`, `sapphire`.

The palette name is a catalog key (`themeAurora`, `themeMidnight`, ...), so every
language names every swatch.

## Contrast

`ThemeCatalog.ContrastForeground` computes a readable foreground from the accent
luminance instead of hardcoding white or black. `ThemeCatalog.Blend` mixes the
accent with the surface for the soft and faint accent brushes, so light and dark
modes stay coherent for any custom hex.

## Custom accent

The settings input accepts a hex value with or without `#`, normalizes it to
uppercase `#RRGGBB`, and rejects anything else with a localized inline message.
The last 12 valid accents are stored as swatches.

## Modes

`System`, `Light`, and `Dark`. System mode reads
`HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
and watches for changes, so the window follows the OS without a restart.

## Accessibility

| Control | Range | Behavior |
|---|---|---|
| Interface scale | 80% to 150% | Scales the whole visual tree, independent of DPI |
| Text scale | 90% to 140% | Additional text-only scaling |
| Reduced motion | On/off | Disables toast and page animations |
| High contrast | On/off | Raises border and muted contrast |

All interactive elements are reachable by keyboard, the wizard and dialogs use a
default button, and icon-only buttons carry a localized tooltip and an
automation name.

## DPI

The project sets `ApplicationHighDpiMode=PerMonitorV2` and the manifest carries no
`dpiAware` element, which avoids the `WFAC010` conflict between WPF and Windows
Forms. The donut chart renders with `VisualTreeHelper.GetDpi(this).PixelsPerDip`
so text stays crisp when the window moves between monitors.
