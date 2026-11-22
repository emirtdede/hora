using System.Runtime.InteropServices;
using System.Text;

namespace Hora.Core.Tracking;

/// <summary>Foreground window information captured once per sample.</summary>
public sealed record ForegroundSample(string Exe, string Title, bool Valid, int ProcessId = 0)
{
    public static ForegroundSample None { get; } = new(string.Empty, string.Empty, false);
}

public interface IForegroundService
{
    ForegroundSample Sample();
}

/// <summary>Reads the active window title and owning process name using Win32.</summary>
public sealed partial class ForegroundService : IForegroundService
{
    public ForegroundSample Sample()
    {
        try
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero)
            {
                return ForegroundSample.None;
            }

            var length = GetWindowTextLength(handle);
            var builder = new StringBuilder(Math.Clamp(length + 2, 8, 1024));
            _ = GetWindowText(handle, builder, builder.Capacity);
            _ = GetWindowThreadProcessId(handle, out var processId);

            var exe = ResolveProcessName(processId);
            return new ForegroundSample(exe, builder.ToString(), !string.IsNullOrEmpty(exe), (int)processId);
        }
        catch (Exception exception) when (exception is InvalidOperationException
                                              or ArgumentException
                                              or System.ComponentModel.Win32Exception
                                              or NotSupportedException)
        {
            return ForegroundSample.None;
        }
    }

    private static string ResolveProcessName(uint processId)
    {
        if (processId == 0)
        {
            return string.Empty;
        }

        try
        {
            using var process = System.Diagnostics.Process.GetProcessById((int)processId);
            var name = process.ProcessName;
            return name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name[..^4] : name;
        }
        catch (Exception exception) when (exception is ArgumentException
                                              or InvalidOperationException
                                              or System.ComponentModel.Win32Exception
                                              or NotSupportedException)
        {
            // The process exited, or it is elevated and cannot be inspected.
            return string.Empty;
        }
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
