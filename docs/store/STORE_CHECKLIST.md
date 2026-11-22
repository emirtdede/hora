# Microsoft Store Checklist

## Package identity

| Field | Value |
|---|---|
| Display name | Hora |
| Publisher display | Vellium |
| Package family | Reserved in Partner Center before first submission |
| Architecture | x64 |
| Minimum OS | Windows 10 version 1809 (build 17763) |
| Category | Productivity |
| Price | Free |

## Required assets

| Asset | Size |
|---|---|
| Store logo | 50 x 50 |
| Small tile | 71 x 71 |
| Medium tile | 150 x 150 |
| Wide tile | 310 x 150 |
| Large tile | 310 x 310 |
| App icon | 44 x 44 |
| Splash screen | 620 x 300 |
| Screenshots | At least 4, 1366 x 768 or larger |

Use the palette in `assets/hora.svg` for all marketing tiles. Do not add text to
the logo: the Store renders the product name separately.

## Listing

- Short description: see `store-listing.json`.
- Description: the Overview section of `README.md`, without badges.
- Privacy policy URL: the published privacy policy.
- Support URL: `https://github.com/emirtdede/hora/issues`.
- Website: `https://vellium.dev`.

## Capabilities and declarations

- The app is a desktop Win32/WPF app packaged as MSIX. It declares no restricted
  capability.
- It does not require an account.
- It does not collect personal information, so the privacy questionnaire answers
  "No" to data collection. The optional GitHub update check is a user-initiated
  request that carries no identifier, and it is disabled in Store builds.

## Behavior in a Store build

`UpdateService.IsStoreBuild` detects an install under
`%LOCALAPPDATA%\Microsoft\WindowsApps` and disables the self-update path. The
settings row states that updates arrive through the Store. Never ship a Store
build that downloads an installer; that is a Store policy violation.

## Pre-submission gate

1. Locale gate passes: `node scripts/check-locales.mjs`.
2. Release build has zero warnings.
3. Full test suite passes.
4. The packaged build starts, shows the wizard, and reaches the dashboard.
5. Autostart writes to `HKCU` only, and no elevation prompt appears.
6. The privacy policy URL resolves.
7. Screenshots show the current version and no placeholder text.
