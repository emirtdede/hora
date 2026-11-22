# Hora 1.0.0 - Fix Log

This file records the remediation work behind [`QA_AUDIT.md`](QA_AUDIT.md). Each
entry lists the defect class, the files touched, and what changed. The audit IDs
match the sections in the QA report.

## Storage and data

| ID | Files | Change |
|---|---|---|
| S1, S2 | `src/Hora.Storage/Db/UsageRepository.cs` | Single write semaphore, one transaction per batch (`AddBatchAsync`), unique temp files, stale temp sweep. |
| S3, S4 | `src/Hora.Storage/Backup/BackupEngine.cs`, `src/Hora.UI/Services/BackupService.cs` | `HasUsage` and `IsMeaningful` guards; restore refuses an empty-usage snapshot. |
| S5 | `src/Hora.Storage/Db/UsageRepository.cs`, `BackupEngine.cs` | Restore uses SQLite `BackupDatabase`, validates and de-duplicates rows, `DELETE` then best-effort `VACUUM`. |
| S6 | `src/Hora.Storage/Backup/BackupEngine.cs` | Invariant `yyyy-MM-dd` backup names; regression test under `th-TH`. |
| S7 | `src/Hora.Storage/Backup/BackupEngine.cs` | Rejects `..` and `\\?\` paths; explicit fallback status. |
| S8 | `src/Hora.Storage/Backup/BackupEngine.cs` | Flush to disk, atomic replace, verified copy fallback. |
| S9 | `src/Hora.Storage/Paths/HoraPaths.cs` | Per-file legacy migration, SQLite backup, `migration.complete` marker. |
| S10, S12 | `src/Hora.Storage/Settings/SettingsStore.cs` | Full sanitize pass, NaN guards, case-insensitive load, `File.Replace` write. |
| S11 | `src/Hora.Core/Logging/FileHoraLogger.cs` | Invariant timestamps, per-write daily path, 7-day prune. |

## Backup behavior

| ID | Files | Change |
|---|---|---|
| B1 | `src/Hora.Storage/Backup/BackupLocationDetector.cs` | Consumer, personal, commercial, home fallback chain. |
| B2 | `src/Hora.Storage/Backup/BackupEngine.cs` | Newest 15 dated files kept; sync and previous files protected. |
| B3 | `src/Hora.Storage/Backup/BackupEngine.cs` | Recovery chain across sync, previous, and dated files. |
| B4, B5 | `src/Hora.UI/Services/BackupService.cs` | Restore before first sync; debounce plus flush on close; dirty marker survives failure. |
| B6, B7 | `src/Hora.UI/Views/BackupView.cs`, `src/Hora.UI/MainWindow.xaml.cs` | Localized toasts; dispatcher marshalling; unsubscribe on unload. |

## UI

| ID | Files | Change |
|---|---|---|
| U1, U4 | `MainWindow.xaml.cs`, `SettingsView.cs`, `AppContext.cs` | Register the shortcut on load; expose `HotkeyAvailable`; report failure honestly. |
| U2, U13, U14 | `MainWindow.xaml.cs`, `BackupView.cs` | Thread-safe status refresh, shutdown guards, wider exception filters. |
| U3 | `BackupService.cs`, `BackupView.cs` | Read inside the guard; JSON and SQLite exceptions handled with a toast. |
| U5, U6 | `MainWindow.xaml.cs`, `SettingsView.cs`, `AppContext.cs` | Debounced geometry save; `SaveSettingsDeferred` for sliders. |
| U7, U8, U9, U10 | `SetupWindow.cs`, `Dialogs.cs`, `MainWindow.xaml(.cs)` | Work-area sizing, scrollable dialogs, Escape handling, on-screen restore, work-area clamp. |
| U11, U12 | `MainWindow.xaml.cs`, `App.xaml.cs` | Flush-driven total refresh with re-entry guard; `_ownsMutex`. |
| U15, U16, U17 | `DiagnosticsView.cs`, `BackupView.cs`, `AboutView.cs` | Probe on the thread pool, formatted placeholders, assembly version. |
| U18, U19 | `BackupView.cs`, `DiagnosticsView.cs`, `SettingsView.cs`, catalogs | Localized check-count and language toast; Behavior help key; measurement units kept as invariant symbols. |
| U20 | `App.xaml.cs`, `MainWindow.xaml.cs` | Recoverable vs fatal dispatcher exception split; clean exit on fatal. |
| U21 | `Theme/Styles.xaml` | Shared `HoraFocusVisual` focus ring. |
| U22, U33 | `SettingsView.cs`, `AboutView.cs`, `DashboardView.cs` | Star columns with min and max, stretch sliders, auto label column. |
| U23, U24, U27 | `MainWindow.xaml.cs`, `ToastService.cs`, `PageBase.cs` | Detached handlers, timer disposal, observed fire-and-forget tasks. |
| U25 | `MainWindow.xaml`, `SettingsView.cs` | Accessible names and tooltips for icon buttons and swatches. |
| U26 | `MainWindow.xaml.cs` | Bounded shutdown flush. |
| U28, U29, U30 | `AppContext.cs`, `HistoryView.cs`, `SettingsView.cs` | One provider mapping, midnight rollover, saved swatch selection. |
| U31, U32 | `DocsView.cs`, `App.xaml.cs` | Guarded link opening, dispatcher shutdown check. |

## Localization and automation

| ID | Files | Change |
|---|---|---|
| L1 | `scripts/translate-locales.mjs`, `scripts/check-locales.mjs` | Marker contamination checks, degenerate rejection, per-key retry, `--keys`, `--only`, `--force`. |
| L2 | `scripts/locales/en.json`, all catalogs | Proprietary Vellium license wording replaces the MIT text. |
| L3 | `.github/workflows/ci.yml`, `release.yml` | SDK matrix includes 8.0.x and 10.0.x. |
| L4 | `docs/screenshots` | Gallery index added, stale capture removed. |
| L5 | `scripts/capture-screenshots.ps1`, `tools/Hora.Seed` | Silent single-pass capture: no maximize, no focus steal, `PrintWindow` pixels, demo rows seeded before launch, process closed after the last frame. |

## Runtime findings from the visual pass

| ID | Files | Change |
|---|---|---|
| R1 | `App.xaml.cs` | `ShutdownMode.OnExplicitShutdown`, so closing the wizard does not end the process before the main window exists. |
| R2 | `MainWindow.xaml.cs` | Recovery prompt and close dialog defer to `ApplicationIdle` and set `Owner` only when the window is visible. |
| R3 | `Core/Tracking/AudioActivityService.cs` | `IUnknown` marshalling for `Activate` and `GetSession`; wider COM catch. |
| R4 | `Services/BackupService.cs`, `Views/BackupView.cs` | Backup summary reports local record count. |
| R5 | `Views/DiagnosticsView.cs` | Dedicated `diagnosticsLogsHelp` subtitle. |
| R6 | `Services/SystemInfoReport.cs` | Report labels, provider name, and booleans are localized. |
| R7 | `installer/hora.nsi`, `scripts/make-installer-languages.mjs` | Installer strings generated from the catalogs; no empty section text; own finish page title and body. |
| R8 | `installer/hora.nsi` | `IfSilent` branches for install, upgrade, and uninstall; no dialogs in silent mode. |
| R9 | `scripts/capture-screenshots.ps1`, `tools/Hora.Seed` | Silent single pass with `PrintWindow`, committed seeding helper, process closed after the last frame. |
| R10 | `Storage/Backup/BackupEngine.cs` | Shared `JsonOptions` for writing and reading backup documents. |

## Verification record

Last local run before tagging, on Windows 11 build 26100, .NET SDK 8.0.4xx.

| Check | Command | Result |
|---|---|---|
| Build | `dotnet build Hora.slnx` | 0 warnings, 0 errors |
| Tests | `dotnet test Hora.slnx -c Release` | 185 passed, 0 failed, 0 skipped |
| Locales | `node scripts/check-locales.mjs` | 73 catalogs, 350 keys each |
| Dash scan | repository scan for em and en dashes | 0 findings |
| Emoji scan | repository scan for emoji ranges | 0 findings |
| Screenshots | `pwsh -NoProfile -File scripts\capture-screenshots.ps1` | 12 frames, silent pass |
| Installer | silent `/S` install and uninstall of the released setup | exit 0, no files, registry keys, or shortcuts left behind |
| Winget | `winget validate installer\winget\manifests\v\Vellium\Hora\1.0.0` | validation succeeded, no warnings |
| CI | GitHub Actions `CI` on `main` and `development` | success |
| Release | GitHub Actions `Release` on tag `1.0.0` | success, three assets attached |

No em dash or en dash is used anywhere in this repository.
