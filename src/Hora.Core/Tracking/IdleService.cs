using System.Runtime.InteropServices;

namespace Hora.Core.Tracking;

public interface IIdleService
{
    TimeSpan GetInactivity();
    bool IsIdle(TimeSpan threshold);
}

public sealed class IdleService : IIdleService
{
    public TimeSpan GetInactivity()
    {
        var info = new LastInputInfo { CbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info))
        {
            return TimeSpan.Zero;
        }

        unchecked
        {
            var elapsed = (uint)Environment.TickCount - info.DwTime;
            if (elapsed > int.MaxValue)
            {
                elapsed = int.MaxValue;
            }

            return TimeSpan.FromMilliseconds(elapsed);
        }
    }

    public bool IsIdle(TimeSpan threshold) => GetInactivity() >= threshold;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint CbSize;
        public uint DwTime;
    }
}
