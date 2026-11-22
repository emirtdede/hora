# Runbook: Database Restore

## When to use

- Hora shows no history after an update.
- The dashboard total resets to zero while `usage.db` exists on disk.
- A diagnostic report says the database failed its integrity check.

## Before you start

1. Close Hora from the tray (Exit), not just the window.
2. Confirm the process is gone: `Get-Process Hora -ErrorAction SilentlyContinue`.
3. Copy `%LOCALAPPDATA%\Hora` to a safe folder. Never debug on the only copy.

## Restore from a backup file

1. Locate the newest usable backup: `hora-sync.json`, then `hora-previous.json`,
   then the newest `hora-backup-YYYY-MM-DD.json` in `<provider root>\Hora`.
2. In Hora: Backup, Restore from cloud. The service writes the current state as
   the previous snapshot first, so the restore itself can be undone.
3. Verify the dashboard total and the history table for today.

## Restore by hand (no working install)

1. Rename the damaged database:
   `Rename-Item "$env:LOCALAPPDATA\Hora\usage.db" "usage.db.damaged"`.
2. Remove the stale WAL files (`usage.db-wal`, `usage.db-shm`) next to it.
3. Start Hora once. It creates an empty database.
4. Use Backup, Restore from cloud to import the snapshot.

## Verify

```sql
SELECT date, SUM(seconds) AS total_seconds
FROM usage
GROUP BY date
ORDER BY date DESC
LIMIT 7;
```

Totals must be positive, and system buckets (`idle`, `locked`, `suspended`,
`meeting`) must not appear inside an active total.

## Escalate

If a snapshot restores but the totals still look wrong, collect a diagnostic
report (About, Copy system information) and open an issue on GitHub with the
report and the backup file's `exportedUtc` value.
