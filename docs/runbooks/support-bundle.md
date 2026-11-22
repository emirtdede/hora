# Runbook: Support Bundle

## What a support bundle contains

The diagnostic report is built by `DiagnosticsService` and copied from the
Diagnostics page. It is designed to be safe to paste into a public issue.

| Section | Content |
|---|---|
| App | Version, build configuration, .NET runtime |
| System | Windows edition and build, architecture, DPI scale |
| Storage | Database size, row count, oldest and newest date |
| Backup | Provider kind, connected flag, last sync time, record count |
| Tracking | Whether tracking is active, last sample age, pending seconds |
| Integrity | Result of `PRAGMA quick_check`, settings parse result |
| Environment | Detected provider folders as booleans, not paths |

## What it never contains

- Window titles.
- Domain names or URLs.
- Executable paths. Only the executable file name appears in usage rows, and the
  report does not list them.
- Settings values that can identify a person, such as a custom folder path or a
  machine name.
- Any token, key, or account identifier. Hora has none.

## Collect

1. Open Hora, Diagnostics.
2. Press "Copy system information".
3. Paste into the issue inside a fenced block.

If the app does not start, read `%LOCALAPPDATA%\Hora\logs\hora-<date>.log`. Logs
are redacted the same way and contain no titles.

## Ask the user for more only when needed

Ask for the exact steps, the expected result, and the actual result. Do not ask
for a database unless the issue is about totals; if it is, ask for a backup file
and remind the user that it contains application names.
