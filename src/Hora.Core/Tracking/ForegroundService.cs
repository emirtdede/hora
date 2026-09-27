using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Hora.Core.Logging;

namespace Hora.Core.Tracking;

public interface IForegroundService
{
    ForegroundWindow GetCurrent();
}

public sealed class ForegroundService : IForegroundService
{
    private readonly IHoraLogger _log;

    public ForegroundService(IHoraLogger log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public ForegroundWindow GetCurrent()
    {
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
            {
                return new ForegroundWindow("unknown", string.Empty, 0);
            }

            GetWindowThreadProcessId(hwnd, out var pid);
            var title = ReadTitle(hwnd);
            var exe = ReadExeName((int)pid);
            return new ForegroundWindow(exe, title, (int)pid);
        }
        catch (Exception ex)
        {
            _log.Error("Foreground okunamadı.", ex);
            return new ForegroundWindow("unknown", string.Empty, 0);
        }
    }

    private static string ReadTitle(IntPtr hwnd)
    {
        try
        {
            var len = GetWindowTextLength(hwnd);
            if (len <= 0 || len > 1024)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString().Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private string ReadExeName(int pid)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            var name = proc.ProcessName;
            return string.IsNullOrWhiteSpace(name) ? "unknown" : name.ToLowerInvariant();
        }
        catch (Exception ex)
        {
            _log.Warn($"PID {pid} exe adı okunamadı: {ex.Message}");
            return "unknown";
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);
}
