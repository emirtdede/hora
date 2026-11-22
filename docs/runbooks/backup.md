# Runbook: Backup

## Verify a backup

1. Open Hora, Backup.
2. Confirm the provider is detected and the badge reads Connected.
3. Check "Backup summary" for a non-zero record count and a recent last-sync
   time.
4. Open the folder and confirm `hora-sync.json` exists and is not zero bytes.

## Force a backup

Press "Back up now". A local toast confirms "Backup completed." A failure keeps
the local data intact and reports "Backup could not be completed. Your data is
safe on this device."

## Restore

1. Backup, Restore from cloud.
2. Confirm the dialog. The current local state is written as the previous
   snapshot before the restore replaces it, so a restore can itself be undone by
   restoring again.
3. The app re-reads settings and usage; the dashboard refreshes immediately.

## Recover a damaged sync file

Hora already falls back to `hora-previous.json` and then to the newest daily
file. If every file is unreadable:

1. Keep the damaged files; do not delete them.
2. Copy `hora-backup-YYYY-MM-DD.json` from another machine or an older date into
   the folder.
3. Restart Hora. The newest valid file is used.
4. Report the corruption with a diagnostic report so the format can be hardened.

## Switch provider

1. Backup, choose the new provider.
2. Connect. Hora restores a remote snapshot first when local data is empty, then
   syncs.
3. Verify the summary, then disconnect the old provider if you want to stop
   writing there.

## Never do this

- Do not point the backup folder inside `%LOCALAPPDATA%\Hora`. The local fallback
  folder is separate on purpose so a reset does not delete the backup.
- Do not edit `hora-sync.json` while Hora is running; the next debounced write
  replaces it.
