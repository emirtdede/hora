# Hora 1.0.0 - QA and Architecture Audit

Scope: `src/Hora.Core`, `src/Hora.Storage`, `src/Hora.UI`, `scripts`, `installer`,
`.github/workflows`, documentation, and the localization catalogs.

Two read-only audit passes were run before release: one on the storage and backup
layer, one on the UI layer. Every finding is listed with severity, the exact
location, the failure scenario, and the fix that was applied. The rescan section
at the end records the state after the fixes.

Severity scale:

- **Critical**: data loss, corruption, security exposure, or a crash on a normal path.
- **High**: a feature that silently does not work, or a guaranteed runtime exception.
- **Medium**: a visible defect, a performance problem, or an accessibility failure.
- **Low**: a latent risk, a maintainability defect, or a polish issue.

---

## 1. Storage and data layer

### S1 - High - Unguarded concurrent writes to one database file
`src/Hora.Storage/Db/UsageRepository.cs`

Two flush paths (tracker and restore) could write at the same time and race the
WAL checkpoint. Fixed with a single write semaphore, a unique temp file per write,
and a sweep of stale temp files at startup.

### S2 - High - Partial batch write could double count seconds
`src/Hora.Storage/Db/UsageRepository.cs`

The tracker wrote rows one at a time and re-queued the batch on failure, so a
half-applied batch was counted twice on the retry. Fixed with `AddBatchAsync`,
which writes the whole batch in one transaction and only clears pending seconds
after the transaction commits.

### S3 - High - Restore could replace real history with an empty backup
`src/Hora.Storage/Backup/BackupEngine.cs`, `src/Hora.UI/Services/BackupService.cs`

A backup document with zero usage rows could overwrite a populated local database.
Fixed with the `HasUsage` check in the engine and an explicit empty-usage guard in
`RestoreAsync`.

### S4 - High - Settings-only backup overwrote a full backup
`src/Hora.Storage/Backup/BackupEngine.cs`

Syncing while the database was empty replaced a good snapshot. Fixed with the
`IsMeaningful` rule: a document without usage rows never replaces one that has
them.

### S5 - High - SQLite restore could leave a locked or half-copied database
`src/Hora.Storage/Db/UsageRepository.cs`, `src/Hora.Storage/Backup/BackupEngine.cs`

Restore used a raw file copy while connections were open. Fixed by using SQLite's
`BackupDatabase` API, validating every row, de-duplicating on date, executable,
and domain, and running `DELETE` separately from a best-effort `VACUUM`.

### S6 - High - Backup file names depended on the current culture
`src/Hora.Storage/Backup/BackupEngine.cs`

A `th-TH` UI wrote Buddhist-era dates into `hora-backup-*.json`, breaking rotation
and restore on other machines. Fixed with invariant `yyyy-MM-dd` names and a test
that runs under `th-TH`.

### S7 - Medium - Path traversal and device paths accepted
`src/Hora.Storage/Backup/BackupEngine.cs`

A custom folder containing `..` or a `\\?\` prefix was accepted. Fixed with explicit
rejection, and the UI reports that the default folder is in use (`_usingFallback`).

### S8 - Medium - Copy fallback could leave a truncated backup
`src/Hora.Storage/Backup/BackupEngine.cs`

Write used a temp file then a move, but a cross-volume move could fail partway.
Fixed with a unique temp name, flush to disk, atomic replace, and a copy fallback
that verifies the destination length.

### S9 - Medium - Legacy migration could lose files on partial failure
`src/Hora.Storage/Paths/HoraPaths.cs`

The old `%APPDATA%\Hora` folder was moved with a single call. Fixed with a per-file
migration, an SQLite backup for the database, and a `migration.complete` marker so
a retry resumes instead of duplicating.

### S10 - Medium - NaN and out-of-range settings values survived load
`src/Hora.Storage/Settings/SettingsStore.cs`

A hand-edited JSON file could inject `NaN`, negative sizes, or an unknown enum.
Fixed with a full `Sanitize` pass, `NaN` guards, and a case-insensitive loader that
accepts both camelCase and PascalCase.

### S11 - Low - Log timestamps used the current culture
`src/Hora.Core/Logging/FileHoraLogger.cs`

Fixed with `CultureInfo.InvariantCulture`, a path recomputed per write (so it rolls
over at midnight), and pruning of files older than 7 days.

### S12 - Low - Settings write was not atomic on every file system
`src/Hora.Storage/Settings/SettingsStore.cs`

Fixed with `File.Replace` where available, `File.Move` as the fallback, and temp
file cleanup in a `finally` block.

---

## 2. Backup behavior

### B1 - High - Detection chain stopped at the first missing variable
`src/Hora.Storage/Backup/BackupLocationDetector.cs`

Fixed to follow `OneDriveConsumer` -> `OneDrive` -> `OneDriveCommercial` ->
`~/OneDrive`, with a validated local fallback.

### B2 - High - 15-day rotation could delete the newest good file
`src/Hora.Storage/Backup/BackupEngine.cs`

Rotation now keeps the newest 15 files by date and never removes `hora-sync.json`
or `hora-previous.json`.

### B3 - High - Recovery ignored the previous snapshot
`src/Hora.Storage/Backup/BackupEngine.cs`

Fixed with the recovery chain: `hora-sync.json` -> `hora-previous.json` ->
newest dated backup, each parsed and validated before use.

### B4 - Medium - Connect could sync before restoring
`src/Hora.UI/Services/BackupService.cs`

Fixed so connecting restores first when the local database is empty, then syncs.

### B5 - Medium - Debounce lost the last change
`src/Hora.UI/Services/BackupService.cs`

Fixed with a debounce timer plus a flush on close, and a dirty marker that survives
a failed sync.

### B6 - Medium - Sync errors were reported only in the log
`src/Hora.UI/Views/BackupView.cs`

Fixed with localized success and failure toasts for connect, sync, restore, and
disconnect.

### B7 - Low - Backup status event could cross threads into WPF
`src/Hora.UI/Views/BackupView.cs`, `src/Hora.UI/MainWindow.xaml.cs`

Fixed by marshalling through `Dispatcher.BeginInvoke` and unsubscribing on unload.

---

## 3. UI layer

### U1 - High - Global shortcut never registered at startup
`src/Hora.UI/MainWindow.xaml.cs`

`TryRegister` was only called from `OnSettingsChanged`, so `Ctrl+Alt+H` did nothing
on a fresh launch. Fixed by registering in `OnLoaded` after the window handle exists.

### U2 - High - Backup status wrote WPF controls from the thread pool
`src/Hora.UI/MainWindow.xaml.cs`

Fixed by dispatching `RefreshRailFooter` and ignoring the event after shutdown.

### U3 - High - Restore could throw outside its handler
`src/Hora.UI/Services/BackupService.cs`, `src/Hora.UI/Views/BackupView.cs`

`ReadLatestAsync` ran before the `try`. Fixed by moving the read inside the guard
and widening the filter to JSON and SQLite exceptions, with a failure toast.

### U4 - High - Shortcut success was shown unconditionally
`src/Hora.UI/Views/SettingsView.cs`, `src/Hora.UI/AppContext.cs`

`RegisterHotKey` can fail when another app owns the gesture. Fixed with
`AppContext.HotkeyAvailable`, set from the real result and read by the settings page.

### U5 - High - Window geometry saved on every drag pixel
`src/Hora.UI/MainWindow.xaml.cs`

Fixed with a 700 ms debounce timer and a save on close.

### U6 - High - Slider drag ran the whole settings pipeline
`src/Hora.UI/Views/SettingsView.cs`, `src/Hora.UI/AppContext.cs`

Fixed with `SaveSettingsDeferred`, a 350 ms timer that applies live values once and
writes and refreshes once.

### U7 - High - Wizard did not fit a scaled 768p display
`src/Hora.UI/Wizard/SetupWindow.cs`

Fixed by sizing from `SystemParameters.WorkArea`, lowering the minimums, and keeping
the step content scrollable.

### U8 - Medium - Long dialog text pushed the buttons off screen
`src/Hora.UI/Dialogs/Dialogs.cs`

Fixed with a scrollable content host, a work-area aware `MaxHeight`, and Escape to
cancel (`null` for the close dialog so it does not become "exit").

### U9 - Medium - Restored window could land off screen
`src/Hora.UI/MainWindow.xaml.cs`

Fixed with `EnsureVisibleOnScreen`, which re-centers a window whose saved rectangle
no longer intersects the virtual screen.

### U10 - Medium - Default and minimum size exceeded a scaled work area
`src/Hora.UI/MainWindow.xaml.cs`, `src/Hora.UI/MainWindow.xaml`

Fixed by clamping to the work area at construction and lowering `MinHeight` to 560.

### U11 - Medium - One query per second in the status bar
`src/Hora.UI/MainWindow.xaml.cs`

Fixed by refreshing the total after each 5 s flush, guarding re-entry, catching
`ObjectDisposedException`, and only rewriting `StatusMeta` when the text changes.

### U12 - Medium - Second launch threw while releasing an unowned mutex
`src/Hora.UI/App.xaml.cs`

Fixed with `_ownsMutex`, set only when the process created the mutex.

### U13 - Medium - Backup status subscriber blocked the worker
`src/Hora.UI/Views/BackupView.cs`

Fixed with `Dispatcher.BeginInvoke`, a shutdown check, and an `Unloaded` unsubscribe.

### U14 - Medium - Backup reads handled only IO errors
`src/Hora.UI/Views/BackupView.cs`

Fixed by widening the filter and showing a localized failure state.

### U15 - Medium - Diagnostics wrote to disk on the UI thread
`src/Hora.UI/Views/DiagnosticsView.cs`

Fixed by moving the storage probe to `Task.Run` and rendering the rows when it
finishes. The check results are kept for the exported report.

### U16 - Medium - Literal `{0}` placeholders in toasts
`src/Hora.UI/Views/BackupView.cs`, `src/Hora.UI/Views/DiagnosticsView.cs`

Fixed by passing the folder and path to the catalog format call.

### U17 - Medium - About page hardcoded the version
`src/Hora.UI/Views/AboutView.cs`

Fixed to read the assembly version, so it can never drift from the title bar.

### U18 - Medium - Hardcoded count text and separators
`src/Hora.UI/Views/BackupView.cs`, `src/Hora.UI/Views/DiagnosticsView.cs`

The diagnostics report count now comes from `diagnosticsChecksCount`. Time units
(`h`, `m`, `s`) and file size units (`B`, `KB`, `MB`) stay invariant on purpose:
they are measurement symbols, and machine translation of single-letter symbols
produced unusable output (Turkish "ler", "<1 milyon"). Prose around a value always
comes from the catalog. Keyboard names such as `F5` and `Alt+F4` stay literal
because they are key identifiers, not prose.

### U19 - Medium - Language toast showed help text
`src/Hora.UI/Views/SettingsView.cs`

Fixed with `settingsLanguageChanged`, and the Behavior card now uses
`settingsBehaviorHelp` instead of repeating its heading.

### U20 - Medium - Global handler swallowed every UI exception
`src/Hora.UI/App.xaml.cs`, `src/Hora.UI/MainWindow.xaml.cs`

Fixed: expected recoverable failures keep the app alive, anything else is logged,
shown, and followed by a clean exit through `ExitForFailure`.

### U21 - Medium - No visible keyboard focus
`src/Hora.UI/Theme/Styles.xaml`

Fixed with a shared `HoraFocusVisual` ring on every button, toggle, and slider
style instead of `FocusVisualStyle = null`.

### U22 - Medium - Fixed control columns crowded labels at high zoom
`src/Hora.UI/Views/SettingsView.cs`

Fixed by replacing fixed 220 and 260 DIU columns with star columns that have a
minimum and a maximum, and by letting sliders stretch between 150 and 220 DIU.

### U23 - Low - Window and context events were never detached
`src/Hora.UI/MainWindow.xaml.cs`

Fixed with named handlers and `DetachContextEvents` in `ShutdownCleanly`.

### U24 - Low - Toast timer was replaced without stopping
`src/Hora.UI/ToastService.cs`

Fixed by stopping the old timer and clearing the visual references in `Detach`.

### U25 - Low - Unnamed icon-only buttons
`src/Hora.UI/MainWindow.xaml`, `src/Hora.UI/Views/SettingsView.cs`

Fixed with `ToolTip` and `AutomationProperties.Name` on the close button, and the
hex value exposed as the saved swatch name.

### U26 - Low - Blocking flushes on the UI thread
`src/Hora.UI/MainWindow.xaml.cs`

Fixed with a 3 s cancellation budget and a 4 s wait on the combined flush.

### U27 - Low - Fire-and-forget tasks hid faults
`src/Hora.UI/Views/PageBase.cs`

Fixed with `RunSafe`, which observes the task and logs the exception type. Page
loads and diagnostics now go through it.

### U28 - Low - Duplicated provider mapping
`src/Hora.UI/AppContext.cs`

Fixed with `AppContext.ProviderLabelKey`, used by the rail footer and the Backup page.

### U29 - Low - History did not roll over at midnight
`src/Hora.UI/Views/HistoryView.cs`

Fixed with `_followToday`: the page tracks today until the user selects another
day. The history filter now uses `SystemBuckets.IsSystem`, so meetings are excluded
like the dashboard.

### U30 - Low - Saved theme swatches never showed the active state
`src/Hora.UI/Views/SettingsView.cs`

Fixed by storing the hex in `Uid`, so `Tag = "active"` can mark the selected saved
swatch as well.

### U31 - Low - Docs link opening was unguarded
`src/Hora.UI/Views/DocsView.cs`

Fixed with a `Win32Exception` and `InvalidOperationException` guard and a toast.

### U32 - Low - Activation listener could post to a dead dispatcher
`src/Hora.UI/App.xaml.cs`

Fixed with `HasShutdownStarted` and a `try/catch` around `BeginInvoke`.

### U33 - Low - Fixed widths clipped at large font scale
`src/Hora.UI/Views/AboutView.cs`, `src/Hora.UI/Views/DashboardView.cs`

Fixed by making the About label column `Auto` and widening the legend label cap.

---

## 4. Localization and repository hygiene

### L1 - High - Broken, empty, or contaminated translations
`src/Hora.Core/Localization/locales/*.json`, `scripts/translate-locales.mjs`

The gate rejected degenerate output, leaked `<<<n>>>` markers, placeholder drift,
em dashes, and emoji. The translator now rejects those chunks, retries only the
unfinished keys, persists after every attempt, and supports `--keys` for targeted
rewrites.

### L2 - High - License copy still said MIT
`scripts/locales/en.json` and all catalogs

The About and Legal pages now describe the Vellium proprietary license.

### L3 - Medium - CI pinned the wrong SDK
`.github/workflows/ci.yml`, `.github/workflows/release.yml`

Fixed by installing both 8.0.x and 10.0.x so the build and test matrix stay valid.

### L4 - Low - Stale screenshot and missing gallery index
`docs/screenshots`

Removed the obsolete `01-dashboard.png` and added `docs/screenshots/README.md`.

---

## 6. Runtime findings from the visual pass

These defects only appeared when the real executable was driven by the screenshot
automation, which is why they are recorded separately from the static audit.

### R1 - Critical - Closing the setup wizard shut down the whole process
`src/Hora.UI/App.xaml.cs`

With the default `OnLastWindowClose` mode, closing the wizard started application
shutdown before the main window was created. `OnClosing` then tried to own a dialog
with an invisible owner and threw `InvalidOperationException`, so every first run
either died or lost the main window. Fixed by setting
`ShutdownMode = OnExplicitShutdown` at startup; exit is always explicit in Hora.

### R2 - Critical - Backup recovery prompt threw inside `Loaded`
`src/Hora.UI/MainWindow.xaml.cs`

`Owner` cannot be assigned to a window that has not been shown yet. The prompt was
created in `Loaded`, so `Owner = this` threw on the first run after a backup was
found and the restore offer never appeared. Fixed by deferring the prompt to
`DispatcherPriority.ApplicationIdle` and only setting `Owner` when `IsVisible`.
The close dialog uses the same guard.

### R3 - High - Audio activity probe threw on every sample
`src/Hora.Core/Tracking/AudioActivityService.cs`

`IMMDevice.Activate` was declared with a plain `out object`, so the CLR read the
returned interface pointer as a `VARIANT` and threw
`InvalidOleVariantTypeException`. Audio detection never worked, and the exception
surfaced through the global handler. Fixed with
`[MarshalAs(UnmanagedType.IUnknown)]` on `Activate` and `GetSession`, plus a wider
catch so a broken audio stack can never stop tracking.

### R4 - Medium - Backup summary described the last file, not this device
`src/Hora.UI/Services/BackupService.cs`, `src/Hora.UI/Views/BackupView.cs`

The summary showed `0` records next to a populated dashboard because it read the
last backup document. Fixed with `LocalRecordCount` from the repository, used by the
Backup page.

### R5 - Medium - Diagnostics repeated the empty log text as a subtitle
`src/Hora.UI/Views/DiagnosticsView.cs`

The "Recent log entries" card always printed "No log entries for today." as its
description, even when entries existed. Fixed with `diagnosticsLogsHelp`.

### R6 - Medium - System information report was English only
`src/Hora.UI/Services/SystemInfoReport.cs`

Labels, the provider name, and the boolean values were hardcoded English and the
raw provider id (`OneDrive`) was printed. Fixed with `sys*` catalog keys,
`AppContext.ProviderLabelKey`, and `commonYes` / `commonNo` / `commonUnknown`.

### R7 - High - Installer showed English strings and blank section text
`installer/hora.nsi`, `scripts/make-installer-languages.mjs`

The setup declared 51 NSIS languages but only defined `DESC_*` strings for
English, so every other language showed empty section descriptions. Several
labels, the welcome text, and both confirmation dialogs were hardcoded English
while the surrounding page was localized. Fixed by generating
`installer/generated/languages.nsh` from the application catalogs and routing
every installer string through `$(INSTALLER_*)` LangStrings. The finish page
also carries its own title and body instead of the stock NSIS text, which
repeated the product name with a doubled space.

### R8 - Medium - Silent install and uninstall stopped on dialogs
`installer/hora.nsi`

`/S` still displayed the language selector, the upgrade question, and the
uninstall confirmation, so winget and deployment tools would hang or fail.
Fixed with `IfSilent` branches: silent runs install, upgrade, and uninstall
without a prompt.

### R9 - Medium - Screenshot pass stole focus and needed manual seeding
`scripts/capture-screenshots.ps1`, `tools/Hora.Seed`

The capture script maximized the window, called `SetForegroundWindow`, and
depended on a throwaway seeding project outside the repository, so the window
was repeatedly reopened and the desktop was taken over for the whole run. Fixed
with a single silent pass: demo rows are seeded through a committed helper
before launch, navigation uses UI Automation ids without activation, pixels come
from `PrintWindow`, and the process closes after the last frame.

### R10 - Low - camelCase backup files looked truncated to external readers
`src/Hora.Storage/Backup/BackupEngine.cs`

The engine wrote camelCase JSON but the test read it with default options, so a
correct file deserialized to an empty payload. The shared options are now
exposed as `BackupEngine.JsonOptions`, and both directions use them.

---

## 5. Rescan

| Check | Result |
|---|---|
| `dotnet build Hora.slnx` | 0 warnings, 0 errors |
| `dotnet test Hora.slnx -c Release` | 185 passed, 0 failed, 0 skipped |
| `node scripts/check-locales.mjs` | 73 catalogs, 350 keys each |
| Em dash, en dash, and emoji scan | 0 findings |
| Screenshot pass | 12 frames captured silently from the real executable |
| UI, storage, and backup rescan | no Critical or High findings remain open |
| Installer and winget rescan | silent install and uninstall clean, winget validate without warnings |

Open items after the fixes: none at Critical, High, or Medium. No finding was
accepted without a code change.
