# Storage Model

## Location

All state lives under `%LOCALAPPDATA%\Hora`. Roaming storage was used by older
builds; `HoraPaths.MigrateLegacyData` copies `settings.json`, `usage.db`, icons,
and reports from `%APPDATA%\Hora` once, when no local profile exists. The local
folder is correct for a database and for logs because neither should roam
between machines.

| File | Purpose |
|---|---|
| `usage.db` | SQLite database in WAL mode |
| `usage.db-wal`, `usage.db-shm` | SQLite write-ahead log and shared memory |
| `settings.json` | All user preferences |
| `logs/hora-YYYYMMDD.log` | Redacted rotating log, 7 files kept |
| `icons/hora-<id>.ico` | Generated tray and shortcut icon |
| `reports/hora-report-*.txt` | Exported diagnostic reports |
| `backup/` | Local fallback backup root |

## Usage table

```sql
CREATE TABLE IF NOT EXISTS usage (
    date   TEXT NOT NULL,   -- yyyy-MM-dd, invariant culture
    exe    TEXT NOT NULL,   -- lower-case executable name, or a system bucket
    domain TEXT NOT NULL,   -- empty string when there is no domain
    seconds INTEGER NOT NULL,
    PRIMARY KEY (date, exe, domain)
);
```

The primary key makes every write an idempotent upsert (`ON CONFLICT ... DO
UPDATE SET seconds = seconds + excluded.seconds`), so a retried flush adds time
exactly once at the row level and never duplicates a key.

System buckets (`idle`, `locked`, `suspended`, `meeting`) share the table but are
excluded from every "active time" total by `UsageSummary`.

## Settings

`AppSettings` is a flat class with a safe default for every property.
`SettingsStore.Sanitize` is applied on load and on save, so a hand-edited or
partial file can never produce an out-of-range window, an unknown language, an
unknown palette, an unknown provider, or an invalid hotkey.

Writes are atomic: the file is written to `settings.json.tmp-<pid>` and then
moved over the original, with a copy fallback when the move is rejected by a
filter driver. A corrupt file loads as defaults instead of failing startup.

## Retention

Usage rows are kept indefinitely by default because they are small (a few
hundred bytes per day per application). Backup files are trimmed to 15 days.
"Delete all data" in Settings removes the database rows and then vacuums the
file.

## Backup export

`BackupService.BuildDocumentAsync` reads every row, the portable subset of
settings, and the last 200 known executables. The document is versioned so a
future reader can migrate it.
