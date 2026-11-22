# Hora Privacy Policy

**Publisher:** Vellium
**Product:** Hora for Windows
**Last updated:** 22 November 2026
**Applies to:** Hora 1.0.0 and later

## Summary

Hora measures how long you use each application and, for supported browsers, how
long you spend on each website. Everything is stored on your own PC. Hora has no
account, no telemetry, no advertising, and no cloud server operated by Vellium.

## What Hora records

- The name of the application in the foreground, sampled once per second.
- The length of time that application was in the foreground, aggregated per day.
- For supported browsers, the website domain taken from the window title.
- Separate totals for idle time, screen lock, sleep, and meetings.

Hora does not record keystrokes, screenshots, clipboard content, file names,
document text, or the content of web pages.

## Window titles and privacy filter

Window titles are read in memory to detect the active application and the
website domain. A privacy filter discards titles from password managers,
banking, payment, medical, and payroll software, and titles that look like card
numbers. A discarded title is replaced with a neutral placeholder before anything
is written to disk. Only application name, domain, and duration are stored.

## Where data is stored

| Location | Content |
|---|---|
| `%LOCALAPPDATA%\Hora\usage.db` | Usage database |
| `%LOCALAPPDATA%\Hora\settings.json` | Your preferences |
| `%LOCALAPPDATA%\Hora\logs` | Redacted diagnostic log, 7 days kept |
| A folder you choose | Optional backup copy |

If you enable a backup folder, Hora writes `hora-sync.json`, `hora-previous.json`,
and daily snapshots (`hora-backup-YYYY-MM-DD.json`, 15 kept) into
`<your folder>\Hora`. If that folder is inside OneDrive, Google Drive, Dropbox, or
iCloud Drive, the client you already installed may sync those files under the
terms you agreed with that provider. Vellium receives nothing.

## Network activity

Hora makes one kind of outbound request: when you press "Check for updates", it
requests `https://api.github.com/repos/emirtdede/hora/releases/latest`. The
request contains no identifier, no machine name, and no usage data. It is not
made automatically and it is disabled entirely for Microsoft Store builds.

Hora has no analytics, no crash reporting service, and no remote logging.

## Your control

- Pause or resume tracking at any time from the window or the tray.
- Exclude applications from tracking.
- Delete all recorded data from Settings, Data. The database is emptied and
  vacuumed.
- Disconnect a backup provider or change the folder at any time.
- Uninstall Hora. Your data is kept unless you also delete
  `%LOCALAPPDATA%\Hora`.

## Children

Hora is a general-purpose productivity tool and is not directed at children. It
collects no personal identifiers.

## Third-party components

Hora uses open-source libraries for SQLite access and Windows integration. These
libraries run locally and transmit nothing.

## Changes

A material change to this policy is published with the release that introduces
it, and the date above is updated.

## Contact

Questions about privacy: open an issue at
`https://github.com/emirtdede/hora/issues` or contact Vellium through
`https://vellium.dev`.
