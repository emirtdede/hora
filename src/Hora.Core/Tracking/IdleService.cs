using System.Runtime.InteropServices;

namespace Hora.Core.Tracking;

public interface IIdleService
{
    /// <summary>Seconds since the last keyboard or mouse input.</summary>
    uint IdleSeconds();
}

/// <summary>Wraps GetLastInputInfo for inactivity detection.</summary>
public sealed partial class IdleService : IIdleService
{
    public uint IdleSeconds()
    {
        try
        {
            var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
            if (!GetLastInputInfo(ref info))
            {
                return 0;
            }

            var elapsed = unchecked((uint)Environment.TickCount - info.Time);
            return Math.Min(elapsed / 1000u, 86_400u);
        }
        catch (Exception exception) when (exception is EntryPointNotFoundException or DllNotFoundException)
        {
            return 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);
}
