# Signing and Checksums

## Current state

The `1.0.0` artifacts are unsigned. Windows SmartScreen therefore shows a
publisher warning on first run. This is a known, documented limitation, not a
defect in the product.

## Adding a code-signing certificate

1. Acquire an EV or OV code-signing certificate from a public CA.
2. Store it as a base64 PFX in the repository secret `WINDOWS_CERT_PFX` and the
   password in `WINDOWS_CERT_PASSWORD`.
3. Add a signing step to `.github/workflows/release.yml` before the upload step:

```yaml
- name: Sign the setup and the executable
  shell: pwsh
  run: |
    $pfx = [IO.File]::WriteAllBytes("$env:RUNNER_TEMP\cert.pfx",
      [Convert]::FromBase64String("${{ secrets.WINDOWS_CERT_PFX }}"))
    & signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
      /f "$env:RUNNER_TEMP\cert.pfx" /p "${{ secrets.WINDOWS_CERT_PASSWORD }}" `
      "dist\Hora-Setup-1.0.0.exe"
```

4. Sign the portable `Hora.exe` before zipping so the extracted binary is signed
   as well.

## Integrity

`dist/SHA256SUMS.txt` lists a lower-case SHA-256 for every artifact. Verify with:

```powershell
Get-FileHash .\Hora-Setup-1.0.0.exe -Algorithm SHA256
```

Compare the value with the manifest and with the value published in the
release notes. The winget installer manifest carries the same hash in
`InstallerSha256`.

## Store builds

Microsoft Store packages are signed by the Store and must not self-update.
`UpdateService.IsStoreBuild` detects an install under `WindowsApps` and disables
the self-update path so the Store remains the only delivery channel.
