# Builds the Hora release artifacts: a portable folder, a portable zip, the
# NSIS setup file, and a SHA-256 manifest. Windows PowerShell 5.1 and
# PowerShell 7 are both supported.
#
# Usage:
#   ./scripts/build-release.ps1 -Version 1.0.0
#   ./scripts/build-release.ps1 -Version 1.0.0 -SkipInstaller

[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root "publish\Hora"
$distDir = Join-Path $root "dist"
$project = Join-Path $root "src\Hora.UI\Hora.UI.csproj"

Write-Host "Hora release $Version" -ForegroundColor Cyan

# Version must stay in lockstep with the assemblies and the README.
$props = Get-Content (Join-Path $root "Directory.Build.props") -Raw
if ($props -notmatch "<Version>$([regex]::Escape($Version))</Version>") {
    throw "Directory.Build.props does not declare Version $Version."
}

if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
if (Test-Path $distDir) { Remove-Item $distDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $distDir -Force | Out-Null

Write-Host "Publishing self-contained win-x64 build..." -ForegroundColor Cyan
dotnet publish $project `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

$exe = Join-Path $publishDir "Hora.exe"
if (-not (Test-Path $exe)) { throw "Publish output is missing Hora.exe." }
$exeVersion = (Get-Item $exe).VersionInfo.ProductVersion
Write-Host "Published Hora.exe version $exeVersion"

$zip = Join-Path $distDir "Hora-Portable-$Version.zip"
Write-Host "Creating $zip" -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zip -CompressionLevel Optimal

if (-not $SkipInstaller) {
    # The setup page reuses the application catalogs, so the languages must be
    # generated from the same commit that produced the build.
    $node = Get-Command node.exe -ErrorAction SilentlyContinue
    if (-not $node) { throw "Node.js is required to generate the installer languages." }
    & $node.Source (Join-Path $root "scripts\make-installer-languages.mjs")
    if ($LASTEXITCODE -ne 0) { throw "Installer language generation failed." }

    $makensis = Get-Command makensis.exe -ErrorAction SilentlyContinue
    if (-not $makensis) {
        $candidates = @(
            "${env:ProgramFiles(x86)}\NSIS\makensis.exe",
            "$env:ProgramFiles\NSIS\makensis.exe",
            "$env:LOCALAPPDATA\Programs\NSIS\makensis.exe"
        )
        foreach ($candidate in $candidates) {
            if (Test-Path $candidate) { $makensis = $candidate; break }
        }
    }

    if (-not $makensis) {
        Write-Warning "NSIS was not found. Skipping the setup executable. Install NSIS 3 to build it."
    }
    else {
        $setup = Join-Path $distDir "Hora-Setup-$Version.exe"
        Write-Host "Compiling $setup" -ForegroundColor Cyan
        $makensisPath = if ($makensis -is [string]) { $makensis } else { $makensis.Source }
        & $makensisPath `
            "/DVERSION=$Version" `
            "/DPUBLISH_DIR=$publishDir" `
            "/DOUT_FILE=$setup" `
            (Join-Path $root "installer\hora.nsi")
        if ($LASTEXITCODE -ne 0) { throw "makensis failed with exit code $LASTEXITCODE." }
    }
}

$sums = Join-Path $distDir "SHA256SUMS.txt"
$lines = Get-ChildItem $distDir -File |
    Where-Object { $_.Name -ne "SHA256SUMS.txt" } |
    Sort-Object Name |
    ForEach-Object {
        $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $($_.Name)"
    }
Set-Content -Path $sums -Value $lines -Encoding ASCII

Write-Host "Artifacts in $distDir" -ForegroundColor Green
Get-ChildItem $distDir | ForEach-Object {
    "  {0}  {1:N1} MB" -f $_.Name, ($_.Length / 1MB)
}
