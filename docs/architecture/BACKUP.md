# Backup Model

## Goal

Keep one portable copy of settings and usage on a folder that a cloud client
already syncs, without an account, OAuth, a subscription, or a server.

## Provider detection

Detection is local only. The order is fixed and documented because the first
match wins:

| Provider | Detection chain |
|---|---|
| OneDrive | `OneDriveConsumer` env, then `OneDrive` env, then `OneDriveCommercial` env, then `%USERPROFILE%\OneDrive` |
| Google Drive | `GoogleDrive` env, then a `Google Drive` or `My Drive` folder in the profile, then a drive root named `My Drive` |
| Dropbox | `Dropbox` env, then `%APPDATA%\Dropbox\info.json` `personal` or `business` path |
| iCloud | `iCloudDrive` env, then `%USERPROFILE%\iCloudDrive` |
| Custom | Any absolute folder the user selects |
| Local | `%LOCALAPPDATA%\Hora\backup\<Provider>` fallback |

Every resolved path goes through `BackupLocationDetector.IsUnsafePath`. Empty,
relative, and traversal paths are rejected, and the service falls back to the
local folder instead of writing somewhere unexpected.

## Layout

```text
<provider root>/Hora/
  hora-sync.json                  Latest snapshot
  hora-previous.json              Snapshot before the latest write
  hora-backup-YYYY-MM-DD.json     One snapshot per day, 15 kept
```

The account-level folder is always `Hora`, so multiple accounts or clients do not
collide and the path stays stable if the user switches providers.

## Write protocol

1. Build the document from live state.
2. If the document is not meaningful and a usable backup already exists, stop
   with `SkippedEmpty`. A good backup is never replaced by nothing.
3. Write to `<file>.tmp-<pid>`.
4. Replace the target with an atomic move; if the move is rejected, copy over it.
5. Rotate: delete `hora-backup-*` older than 15 days.
6. Record `LastSyncUtc` in settings.

Writes are debounced by 350 ms and coalesced, and a flush runs on close so the
last change is not lost.

## Recovery chain

`ReadLatestAsync` tries, in order:

1. `hora-sync.json`
2. `hora-previous.json`
3. The newest `hora-backup-YYYY-MM-DD.json`

A parse failure, a wrong `app` value, or an empty payload moves to the next
candidate. When recovery had to skip a file, the shell shows the format-recovery
dialog once so the user knows the primary file was unreadable and what was used
instead.

## Connect protection

Connecting a provider never overwrites a populated remote backup with an empty
local state. If a restorable backup exists and the local database is empty, the
remote snapshot is restored first, and only then is the connection marked
connected and a fresh sync scheduled.
