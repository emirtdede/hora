# Contributing to Hora

## Ground rules

- The product name is **Hora**; the publisher is **Vellium**.
- Version stays **1.0.0** until a release decision is made. Do not bump it in a
  pull request.
- Never use em or en dashes in user-facing text. Use a plain hyphen (`-`).
- Never ship an emoji in the application UI or in a locale catalog.
- Every user-facing string must come from the localization catalog. English
  text must never leak into a translated locale.
- Only `main` and `development` branches are kept. Feature work merges into
  `development`.

## Development setup

Requirements: Windows 10 1809 or newer, .NET SDK 8.0 or newer, Node.js 20 or
newer (scripts only), and NSIS 3 (installer only).

```powershell
dotnet build Hora.slnx -c Debug
dotnet test Hora.slnx -c Debug
node scripts/check-locales.mjs
```

## Changes that touch localization

1. Add the key to `scripts/locales/en.json`.
2. Run `node scripts/translate-locales.mjs --only=<ids>` for the affected
   locales, or run it without `--only` for everything.
3. Run `node scripts/check-locales.mjs`. It fails on a missing key, an empty
   value, a dash, an emoji, or a placeholder drift.

Keys live in `src/Hora.Core/Localization/locales/<id>.json` and are embedded in
`Hora.Core`. The English catalog is the source of truth.

## Changes that touch theming

Palettes live in `ThemeCatalog`. A palette has an id, an accent, and a soft
accent. The foreground contrast is computed, never hardcoded. Add the palette
name key to every catalog through the translator.

## Pull request checklist

- `dotnet build Hora.slnx` reports zero warnings and zero errors.
- `dotnet test Hora.slnx` passes.
- `node scripts/check-locales.mjs` passes.
- New logic has a unit test in `tests/Hora.Tests`.
- No hardcoded user-facing string, no `Console.WriteLine`, no secret.
- Screenshots are refreshed when a view changes.
