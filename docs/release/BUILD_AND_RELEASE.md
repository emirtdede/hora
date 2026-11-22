# Build and Release

## Prerequisites

- Windows 10 version 1809 or newer, x64.
- .NET SDK 8.0 or newer.
- Node.js 20 or newer for the scripts.
- NSIS 3 for the setup executable.

## Local build

```powershell
dotnet restore Hora.slnx
dotnet build Hora.slnx -c Release
dotnet test Hora.slnx -c Release
node scripts/check-locales.mjs
```

`Directory.Build.props` sets `TreatWarningsAsErrors`. A warning fails the build on
purpose: the analyzers run at `latest` and the project ships clean.

## Version

The version is `1.0.0` in four places, and all four must agree:

- `Directory.Build.props`: `Version`, `AssemblyVersion`, `FileVersion`.
- `installer/hora.nsi`: default `VERSION` define.
- `.github/workflows/release.yml`: artifact names.
- `README.md`: the release badge.

`scripts/build-release.ps1` refuses to run when `Directory.Build.props` does not
declare the requested version.

## Packaging

```powershell
./scripts/build-release.ps1 -Version 1.0.0
```

Produces:

| File | Content |
|---|---|
| `dist/Hora-Setup-1.0.0.exe` | NSIS setup, per-user, multi-language page |
| `dist/Hora-Portable-1.0.0.zip` | Self-contained single-file build |
| `dist/SHA256SUMS.txt` | Lower-case SHA-256 for every artifact |

The setup is per-user (`RequestExecutionLevel user`), so no elevation prompt
appears. It registers an uninstaller under
`HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\Hora`, writes the
optional Run entry for autostart, and keeps user data on uninstall unless the
user removes it from Settings.

Silent installs (`/S`, winget, deployment tools) never show a dialog: the
language selector, the upgrade question, and the uninstall confirmation are all
skipped, and an existing installation is removed before the new files are
written.

## Installer languages

The setup page reuses the application catalogs, so it never shows a dialog in
English next to a Turkish app. `scripts/make-installer-languages.mjs` reads
`src/Hora.Core/Localization/locales/<id>.json` and writes
`installer/generated/languages.nsh` with one `MUI_LANGUAGE` block per NSIS
translation and the installer strings for each of them. `build-release.ps1`
runs the generator before `makensis`, so the shipped setup always matches the
committed catalogs.

A catalog without an NSIS translation falls back to English strings inside an
English installer page rather than showing an empty string.

## Winget manifest

`installer/winget/manifests/v/Vellium/Hora/1.0.0/` describes the published
setup. The `InstallerSha256` value must equal the asset attached to the `1.0.0`
release, and a setup compiled on another machine differs byte for byte even
from the same source (the NSIS banner embeds a timestamp). Take the hash from
`SHA256SUMS.txt` in the finished Release workflow run and write it into
`Vellium.Hora.installer.yaml`; never copy the hash of a local `dist/` build.

## Screenshot capture

`scripts/capture-screenshots.ps1` drives the real executable through UI
Automation ids and captures pixels with `PrintWindow(PW_RENDERFULLCONTENT)`.
The window is never maximized and never brought to the foreground, and the
process is closed as soon as the last page is saved. Demo rows come from
`tools/Hora.Seed`, which is a development helper only and is not shipped.

## CI

`.github/workflows/ci.yml` runs on pushes and pull requests to `main` and
`development`:

1. Localization gate.
2. Restore, build (Release), test.
3. Upload the TRX file.

## Release

`.github/workflows/release.yml` runs on a `1.0.0` tag:

1. Localization gate and tests.
2. Install NSIS.
3. Build artifacts.
4. Upload artifacts and publish the GitHub release.

## Icon generation

`node scripts/make-icon.mjs` regenerates `assets/hora.ico` with nine sizes
(16 to 256) and 4x supersampling. The runner is pure Node, so the icon can be
rebuilt on a clean machine without design tools.
