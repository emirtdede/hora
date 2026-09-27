#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$InstallDir = Join-Path $env:LOCALAPPDATA "Hora"
$DataDir = Join-Path $env:APPDATA "Hora"

Get-Process -Name "Hora" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath("Programs")) "Hora.lnk") -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hora" -Recurse -Force -ErrorAction SilentlyContinue

try {
    $run = Get-Item -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -ErrorAction Stop
    if ($run.GetValue("Hora")) {
        Remove-ItemProperty -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" -Name "Hora" -Force
    }
} catch { }

if (Test-Path -LiteralPath $DataDir) {
    $answer = Read-Host "Kullanim verilerin de silinsin mi? ($DataDir) [E/h]"
    if ($answer -eq "" -or $answer -eq "E" -or $answer -eq "e") {
        Remove-Item -LiteralPath $DataDir -Recurse -Force
        Write-Output "Veriler silindi."
    } else {
        Write-Output "Veriler korundu: $DataDir"
    }
}

Remove-Item -LiteralPath $InstallDir -Recurse -Force -ErrorAction SilentlyContinue
Write-Output "Hora kaldirildi."
