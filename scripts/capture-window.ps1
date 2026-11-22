param(
    [string]$OutDir = "docs\screenshots",
    [string]$Name = "window",
    [string]$ProcessName = "Hora"
)

# Captures a window by asking it to paint itself, so an overlapping window
# does not end up in the screenshot.
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class HoraShot {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out RECT value, int size);
}
"@

$process = Get-Process -Name $ProcessName -ErrorAction Stop | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $process) { throw "No window found for $ProcessName." }

$handle = $process.MainWindowHandle
[void][HoraShot]::SetForegroundWindow($handle)
Start-Sleep -Milliseconds 500

$rect = New-Object HoraShot+RECT
$size = [System.Runtime.InteropServices.Marshal]::SizeOf($rect)
if ([HoraShot]::DwmGetWindowAttribute($handle, 9, [ref]$rect, $size) -ne 0) {
    [void][HoraShot]::GetWindowRect($handle, [ref]$rect)
}

$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -le 0 -or $height -le 0) { throw "Window has no drawable size." }

$bitmap = New-Object System.Drawing.Bitmap($width, $height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$hdc = $graphics.GetHdc()
# 2 = PW_RENDERFULLCONTENT, required for DirectComposition surfaces such as WPF.
$ok = [HoraShot]::PrintWindow($handle, $hdc, 2)
$graphics.ReleaseHdc($hdc)
$graphics.Dispose()
if (-not $ok) { throw "PrintWindow failed." }

if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }
$file = Join-Path $OutDir "$Name.png"
$bitmap.Save($file, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmap.Dispose()

Write-Host "Saved $file ($width x $height)"
