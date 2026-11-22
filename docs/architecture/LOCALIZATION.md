# Localization Model

## Format

Each language is a flat JSON object embedded in `Hora.Core` as a manifest
resource named `Hora.Core.Localization.locales.<id>.json`.

```json
{
  "appName": "Hora",
  "navDashboard": "Pano"
}
```

Keys are English identifiers, values are fully translated strings. Placeholders
use `{0}`, `{1}`, and so on, and the set must match English exactly.

## Source of truth

`scripts/locales/en.json`. Adding a UI string means:

1. Add the key and the English text to `scripts/locales/en.json`.
2. Run `node scripts/translate-locales.mjs --only=<ids>` for the languages you
   want, or run it with no argument for all 73.
3. Copy the regenerated `en.json` into `src/Hora.Core/Localization/locales/`
   (the generator does this automatically).
4. Run `node scripts/check-locales.mjs`.

## Generator

`scripts/translate-locales.mjs` translates in batches of eight with `<<<n>>>`
markers so a response can be mapped back to the key. It:

- Persists after every batch, so an interrupted run keeps its progress.
- Retries a failed batch with exponential backoff and honors `Retry-After`.
- Re-requests a dropped marker on its own instead of leaving English behind.
- Sanitizes em and en dashes to `-`, strips emoji, and repairs missing
  placeholders.
- Rejects a degenerate result (a string that lost all of its letters) and
  retries, because a punctuation-only string is worse than no translation.

Environment knobs: `HORA_LOCALE_DELAY_MS` (default 400) and
`HORA_LOCALE_ATTEMPTS` (default 6).

## Gates

`scripts/check-locales.mjs` fails on:

- A missing catalog file.
- A missing key or an empty value.
- An em dash, en dash, or horizontal bar.
- An emoji or a variation selector.
- A placeholder set that differs from English.
- A degenerate translation.
- An unknown key that no longer exists in English.

`Hora.Tests.LocalizationGateTests` asserts the same rules inside .NET, so a
catalog edit that skips the script still fails CI.

## Runtime

`Translator` loads a catalog lazily, caches it, and falls back to English only
when a key is genuinely absent. `Translator.Instance` exposes `Get` and `Format`.
`LocalizationProxy` projects `Translator` into XAML bindings and raises a
property change for `Item[]` after a language switch.

Right-to-left languages (`ar`, `he`, `fa`, `ur`) are marked in `LanguageCatalog`
so layout can mirror flow direction.

## Mapping

| App id | Google code | .NET culture |
|---|---|---|
| `zh-tw` | `zh-TW` | `zh-TW` |
| `fil` | `tl` | `fil-PH` |
| `no` | `no` | `nb-NO` |
| `he` | `iw` | `he` |
