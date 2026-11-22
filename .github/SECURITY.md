# Security Policy

## Supported versions

| Version | Supported |
|---------|-----------|
| 1.0.x   | Yes       |

## Reporting a vulnerability

Do not open a public issue for a security problem. Report it privately through
GitHub Security Advisories on this repository, or by email to
security@vellium.dev.

Include the Hora version, the Windows build, reproduction steps, and the impact
you observed. We acknowledge reports within two business days and publish a
fix or mitigation as soon as it is verified.

## Design commitments

- No telemetry, no account, and no cloud server.
- Usage data stays in the local SQLite database unless the user explicitly
  selects a backup folder.
- Backup files are written atomically and never replace a good backup with an
  empty payload.
- Logs are redacted before they reach disk: window titles, domains, and paths
  are removed.
- The updater reads releases from the public GitHub API and never executes an
  unsigned package.
- Microsoft Store builds disable self-update entirely.

## Scope

In scope: the Hora executable, the installer, the update feed handling, the
backup format, and local data handling. Out of scope: third-party sync clients
(OneDrive, Google Drive, Dropbox, iCloud) and the operating system itself.
