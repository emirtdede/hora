param(
    [string]$OutDir = "docs\screenshots",
    [string]$Exe = "",
    [switch]$KeepProfile,
    [switch]$PagesOnly,
    [switch]$SkipSeed
)

# Captures the first-run wizard and every main page in one silent pass.
#
# The window is never maximized and never brought to the foreground: the script
# drives the app through UI Automation ids and reads pixels with
# PrintWindow(PW_RENDERFULLCONTENT), which asks the window to paint itself. The
# user can keep working in another window while the capture runs, and the app
# process is closed as soon as the last frame is saved.
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class HoraCapture {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
}
"@

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $Exe) {
    $Exe = Join-Path $root "src\Hora.UI\bin\Debug\net8.0-windows10.0.17763\Hora.exe"
}
if (-not (Test-Path $Exe)) { throw "Hora.exe not found at $Exe" }

$appDir = Join-Path $env:LOCALAPPDATA "Hora"
$legacyDir = Join-Path $env:APPDATA "Hora"
Get-Process Hora -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 600

if (-not $KeepProfile) {
    Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $legacyDir -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $appDir -Force | Out-Null
    $seed = [ordered]@{
        Left = 120; Top = 90; Width = 1180; Height = 780; Opacity = 1.0; View = "List"
        Language = "en"; Theme = "aurora"; ThemeMode = "Dark"; CustomAccent = "#7C8CF8"
        SavedThemes = @(); IconId = "aurora"; LaunchOnStartup = $false; SilentStart = $false
        CloseBehavior = "tray"; JumpList = $true; Notifications = $true; NotificationSound = $true
        Hotkey = "Ctrl+Alt+H"; UiScale = 1.0; FontScale = 1.0; ReducedMotion = $false
        HighContrast = $false; BackupProvider = "OneDrive"; BackupCustomRoot = ""
        BackupConnected = $false; AutoSync = $false; CompletedSetup = $false
        RememberCloseChoice = $false
    }
    $json = $seed | ConvertTo-Json -Depth 4
    [System.IO.File]::WriteAllText((Join-Path $appDir "settings.json"), $json, (New-Object System.Text.UTF8Encoding($false)))
}

# Demo rows are written before the app starts, so the wizard and the pages are
# captured in the same window lifetime. The tool is a development helper and is
# never shipped with the application.
if (-not $KeepProfile -and -not $SkipSeed -and -not $PagesOnly) {
    $seedProject = Join-Path $root "tools\Hora.Seed\Hora.Seed.csproj"
    if (Test-Path $seedProject) {
        & dotnet run --project $seedProject -c Debug --nologo -v q
        if ($LASTEXITCODE -ne 0) { throw "Demo data seeding failed." }
    }
    else {
        Write-Warning "tools\Hora.Seed is missing; pages will be captured without demo data."
    }
}

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
$OutDir = (Resolve-Path $OutDir).Path

function Save-Shot([IntPtr]$handle, [string]$name) {
    $rect = New-Object HoraCapture+RECT
    $size = [System.Runtime.InteropServices.Marshal]::SizeOf($rect)
    # DWMWA_EXTENDED_FRAME_BOUNDS excludes the invisible resize border, so the
    # saved image matches the visible window exactly.
    if ([HoraCapture]::DwmGetWindowAttribute($handle, 9, [ref]$rect, $size) -ne 0) {
        if (-not [HoraCapture]::GetWindowRect($handle, [ref]$rect)) {
            throw "Could not read the window bounds."
        }
    }

    $width = $rect.Right - $rect.Left
    $height = $rect.Bottom - $rect.Top
    if ($width -le 0 -or $height -le 0) { throw "The window has no drawable size." }

    # PrintWindow occasionally returns before WPF has composited the new page.
    # Three attempts with a short gap is enough and keeps the pass silent.
    $bitmap = $null
    $captured = $false
    for ($attempt = 0; $attempt -lt 3 -and -not $captured; $attempt += 1) {
        $bitmap = New-Object System.Drawing.Bitmap($width, $height)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $graphics.GetHdc()
        # 2 = PW_RENDERFULLCONTENT, required for DirectComposition surfaces such as WPF.
        $captured = [HoraCapture]::PrintWindow($handle, $hdc, 2)
        $graphics.ReleaseHdc($hdc)
        $graphics.Dispose()
        if (-not $captured) {
            $bitmap.Dispose()
            $bitmap = $null
            Start-Sleep -Milliseconds 300
        }
    }

    if (-not $captured -or $null -eq $bitmap) { throw "PrintWindow failed for $name." }
    $bitmap.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmap.Dispose()
    Write-Host ("Saved {0} ({1}x{2})" -f $name, $width, $height)
}

function Find-ById([System.Windows.Automation.AutomationElement]$root, [string]$id) {
    $condition = New-Object System.Windows.Automation.PropertyCondition(
        [System.Windows.Automation.AutomationElement]::AutomationIdProperty, $id)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}

function Invoke-ById([System.Windows.Automation.AutomationElement]$root, [string]$id) {
    $element = Find-ById $root $id
    if (-not $element) { throw "Element with automation id '$id' was not found." }
    $pattern = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    $pattern.Invoke()
}

function Get-Root([IntPtr]$handle) {
    for ($attempt = 0; $attempt -lt 20; $attempt += 1) {
        try {
            $element = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
            if ($element) { return $element }
        }
        catch {
            # The handle can move between the wizard and the main window; the
            # automation peer is briefly unavailable during that swap.
        }
        Start-Sleep -Milliseconds 200
    }

    throw "The window automation tree did not become available."
}

# Waits for a window that exposes a known automation id. The app is never
# activated, so this also covers a window that opens behind the active one.
function Wait-Window([System.Diagnostics.Process]$process, [string]$id, [int]$milliseconds) {
    $deadline = (Get-Date).AddMilliseconds($milliseconds)
    while ((Get-Date) -lt $deadline) {
        $process.Refresh()
        $handle = $process.MainWindowHandle
        if ($handle -ne 0) {
            try {
                $element = [System.Windows.Automation.AutomationElement]::FromHandle($handle)
                if ($element -and (Find-ById $element $id)) {
                    return @{ Handle = $handle; Root = $element }
                }
            }
            catch {
                # A handle from the closing window; retry with the next one.
            }
        }
        Start-Sleep -Milliseconds 300
    }

    throw "The window exposing '$id' did not appear in time."
}

$process = Start-Process -FilePath $Exe -PassThru
try {
    $window = $null
    if ($PagesOnly) {
        $window = Wait-Window $process "Navdashboard" 25000
    }
    else {
        $window = Wait-Window $process "WizardNext" 25000
        Start-Sleep -Milliseconds 900
        $handle = $window.Handle
        $root = $window.Root
        $wizardSteps = @("01-wizard-language", "02-wizard-theme", "03-wizard-welcome", "04-wizard-tour")
        for ($index = 0; $index -lt $wizardSteps.Count; $index += 1) {
            Save-Shot $handle $wizardSteps[$index]
            Invoke-ById $root "WizardNext"
            Start-Sleep -Milliseconds 900
            if ($index -lt $wizardSteps.Count - 1) {
                $root = Get-Root $handle
            }
        }

        # The last step closes the wizard and opens the main window.
        $window = Wait-Window $process "Navdashboard" 25000
        Start-Sleep -Milliseconds 1200
    }

    $handle = $window.Handle
    $root = $window.Root

    $pages = @(
        @{ Id = "dashboard"; Name = "05-dashboard" },
        @{ Id = "history"; Name = "06-history" },
        @{ Id = "settings"; Name = "07-settings" },
        @{ Id = "backup"; Name = "08-backup" },
        @{ Id = "diagnostics"; Name = "09-diagnostics" },
        @{ Id = "docs"; Name = "10-docs" },
        @{ Id = "about"; Name = "11-about" },
        @{ Id = "legal"; Name = "12-legal" }
    )

    foreach ($page in $pages) {
        Invoke-ById $root ("Nav" + $page.Id)
        Start-Sleep -Milliseconds 1100
        Save-Shot $handle $page.Name
    }
}
finally {
    # Close the window immediately after the last frame; nothing is left open.
    Get-Process -Id $process.Id -ErrorAction SilentlyContinue | Stop-Process -Force
}

Write-Host "Capture complete: $OutDir"
