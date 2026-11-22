# Hora - Documentation

This folder holds the current architecture, data contracts, release procedure,
and operational runbooks for Hora. Historical plans and audit logs live in
[`QA_AUDIT.md`](QA_AUDIT.md) and [`FIXES.md`](FIXES.md).

> **Single source of truth:** the working code and the test suite. If a document
> disagrees with the code, the code is right and the document must be fixed.

## Quick access

| Topic | Document |
|---|---|
| Product overview and installation | [`../README.md`](../README.md) |
| Security policy | [`../.github/SECURITY.md`](../.github/SECURITY.md) |
| Contributing | [`../.github/CONTRIBUTING.md`](../.github/CONTRIBUTING.md) |
| Quality audit report | [`QA_AUDIT.md`](QA_AUDIT.md) |
| Fix log | [`FIXES.md`](FIXES.md) |
| Screenshots | [`screenshots/`](screenshots) |

## Architecture (`architecture/`)

| Document | Scope |
|---|---|
| [`TRACKING.md`](architecture/TRACKING.md) | Sampling loop, state priority, aggregation, flush policy |
| [`STORAGE.md`](architecture/STORAGE.md) | SQLite schema, settings file, migration, retention |
| [`BACKUP.md`](architecture/BACKUP.md) | Provider detection, file format, rotation, recovery chain |
| [`LOCALIZATION.md`](architecture/LOCALIZATION.md) | Catalog format, generation, gates, RTL |
| [`THEMING_ACCESSIBILITY.md`](architecture/THEMING_ACCESSIBILITY.md) | Palettes, contrast, zoom, reduced motion |
| [`WINDOW_AND_VIEWPORT_MATRIX.md`](architecture/WINDOW_AND_VIEWPORT_MATRIX.md) | Supported window sizes and resize behavior |
| [`KEYBOARD_SHORTCUTS.md`](architecture/KEYBOARD_SHORTCUTS.md) | Shortcut map |

## Release (`release/`)

| Document | Scope |
|---|---|
| [`BUILD_AND_RELEASE.md`](release/BUILD_AND_RELEASE.md) | Build, test, package, publish |
| [`SIGNING_AND_CHECKSUMS.md`](release/SIGNING_AND_CHECKSUMS.md) | Signing expectations and SHA-256 manifest |
| [`UPDATE_CHANNEL.md`](release/UPDATE_CHANNEL.md) | Update check behavior and Store opt-out |

## Operations (`runbooks/`)

| Document | When to use |
|---|---|
| [`backup.md`](runbooks/backup.md) | Backup verification and recovery |
| [`db-restore.md`](runbooks/db-restore.md) | Restoring the database |
| [`rollback.md`](runbooks/rollback.md) | Rolling back a release |
| [`incident-response.md`](runbooks/incident-response.md) | Security or data incident |
| [`support-bundle.md`](runbooks/support-bundle.md) | Collecting a redacted diagnostic report |

## Store (`store/`)

- [`PRIVACY_POLICY.md`](store/PRIVACY_POLICY.md)
- [`STORE_CHECKLIST.md`](store/STORE_CHECKLIST.md)
- [`store-listing.json`](store/store-listing.json)

## Languages

73 complete catalogs ship in `src/Hora.Core/Localization/locales/<id>.json`.
Parity, dash, emoji, and placeholder rules are enforced twice:

- `node scripts/check-locales.mjs` before a build.
- `Hora.Tests.LocalizationGateTests` inside the test suite.

Adding a key means adding it to `scripts/locales/en.json`, running the
translator, and letting both gates pass.
