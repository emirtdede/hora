using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Hora.Core.Tracking;

public interface IFullscreenService
{
    bool IsForegroundFullscreen(IntPtr hwnd);
}

public sealed class FullscreenService : IFullscreenService
{
    private const int TolerancePx = 4;

    public bool IsForegroundFullscreen(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            if (!GetWindowRect(hwnd, out var rect))
            {
                return false;
            }

            var screen = Screen.FromHandle(hwnd);
            var b = screen.Bounds;
            var w = rect.Right - rect.Left;
            var h = rect.Bottom - rect.Top;
            return Math.Abs(w - b.Width) <= TolerancePx && Math.Abs(h - b.Height) <= TolerancePx;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
