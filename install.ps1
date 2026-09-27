#Requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$InstallDir = Join-Path $env:LOCALAPPDATA "Hora"
$Exe = Join-Path $InstallDir "Hora.exe"
$Source = Join-Path $PSScriptRoot "publish\Hora\Hora.exe"

if (-not (Test-Path -LiteralPath $Source)) {
    Write-Error "publish\Hora\Hora.exe bulunamadi. Once publish alin: dotnet publish src/Hora.UI/Hora.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/Hora"
}

Get-Process -Name "Hora" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
Copy-Item -LiteralPath $Source -Destination $Exe -Force

$StartMenu = Join-Path ([Environment]::GetFolderPath("Programs")) "Hora.lnk"
$Shell = New-Object -ComObject WScript.Shell
$Shortcut = $Shell.CreateShortcut($StartMenu)
$Shortcut.TargetPath = $Exe
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.Description = "Hora - kullanim takibi widget"
$Shortcut.Save()

$UninstallKey = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Hora"
New-Item -Path $UninstallKey -Force | Out-Null
Set-ItemProperty -Path $UninstallKey -Name "DisplayName" -Value "Hora"
Set-ItemProperty -Path $UninstallKey -Name "DisplayVersion" -Value "1.0.0"
Set-ItemProperty -Path $UninstallKey -Name "Publisher" -Value "Hora"
Set-ItemProperty -Path $UninstallKey -Name "InstallLocation" -Value $InstallDir
Set-ItemProperty -Path $UninstallKey -Name "UninstallString" -Value "powershell -ExecutionPolicy Bypass -File `"$InstallDir\uninstall.ps1`""
Copy-Item -LiteralPath (Join-Path $PSScriptRoot "uninstall.ps1") -Destination (Join-Path $InstallDir "uninstall.ps1") -Force

Write-Output "Hora kuruldu: $Exe"
Write-Output "Baslat menusu kisayolu olusturuldu. Simdi baslatiliyor..."
Start-Process -FilePath $Exe
