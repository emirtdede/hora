using System.Runtime.InteropServices;

namespace Hora.Core.Tracking;

public interface IFullscreenService
{
    bool IsFullscreen();
}

/// <summary>Detects when a fullscreen application covers the primary monitor.</summary>
public sealed partial class FullscreenService : IFullscreenService
{
    public bool IsFullscreen()
    {
        try
        {
            var handle = GetForegroundWindow();
            if (handle == IntPtr.Zero || !GetClientRect(handle, out var client))
            {
                return false;
            }

            var monitor = MonitorFromWindow(handle, MonitorDefaultToNearest);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref info))
            {
                return false;
            }

            var width = info.Monitor.Right - info.Monitor.Left;
            var height = info.Monitor.Bottom - info.Monitor.Top;
            // The client area, not the window frame: a maximized window keeps its
            // title bar and taskbar gap, while a fullscreen app covers both.
            var clientWidth = client.Right - client.Left;
            var clientHeight = client.Bottom - client.Top;
            return clientWidth >= width && clientHeight >= height;
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return false;
        }
    }

    private const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRect(IntPtr hWnd, out Rect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
}
