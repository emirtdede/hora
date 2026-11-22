# Update Channel

## Behavior

`UpdateService.CheckAsync` performs one HTTPS GET against
`https://api.github.com/repos/emirtdede/hora/releases/latest` with a 12 second
timeout and a `Hora-Updater/1.0` user agent. It compares the release `tag_name`
with the running assembly version.

| Result | UI |
|---|---|
| Remote version greater than local | "Version {0} is available" with a link to the release |
| Equal or lower | "You are running the latest version" |
| Network or parse failure | "Could not reach the update service. Try again later" |

## What it does not do

- It does not download a package.
- It does not execute anything it fetched.
- It does not carry usage data. The request contains no identifier, no machine
  name, and no settings.
- It does not run on a schedule. The check runs when the user asks for it.

## Store builds

When Hora is installed under `%LOCALAPPDATA%\Microsoft\WindowsApps`, the Store
owns delivery. `SelfUpdateSupported` returns false, the check is skipped, and the
settings row explains that updates arrive through the Store.

## Release contract

Every release must:

- Be tagged `1.0.0` (no `v` prefix) so `Version.TryParse` succeeds after the
  optional `v` trim.
- Publish `Hora-Setup-<version>.exe` and `Hora-Portable-<version>.zip`.
- Publish `SHA256SUMS.txt`.
- Keep the tag readable by `ParseVersion`; an unparsable tag is treated as `0.0.0`
  and never triggers an update prompt.

## Rollback

Because updates are user-driven, a bad release is handled by unpublishing the
release and publishing a corrected patch with the same or a higher version. The
previous installer remains available in the release history.
