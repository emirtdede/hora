using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Hora.Core.Tracking;

public interface ISessionStateService
{
    /// <summary>True when the workstation is locked or the screen saver is running.</summary>
    bool IsLocked();

    /// <summary>True when the workstation is in sleep or hybrid sleep.</summary>
    bool IsSuspended();
}

/// <summary>
/// Tracks lock and suspend state so the timer stops while the PC is not in use.
/// Reads the state directly and also follows Windows session notifications.
/// </summary>
public sealed class SessionStateService : ISessionStateService, IDisposable
{
    private int _locked;
    private int _suspended;
    private bool _disposed;

    public SessionStateService()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        if (TryDetectLocked())
        {
            _locked = 1;
        }
    }

    public bool IsLocked() => Volatile.Read(ref _locked) == 1;

    public bool IsSuspended() => Volatile.Read(ref _suspended) == 1;

    public void SetLocked(bool locked) => Volatile.Write(ref _locked, locked ? 1 : 0);

    public void SetSuspended(bool suspended) => Volatile.Write(ref _suspended, suspended ? 1 : 0);

    private static bool TryDetectLocked()
    {
        try
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("LogonUI");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                SetLocked(true);
                break;
            case SessionSwitchReason.SessionUnlock:
                SetLocked(false);
                SetSuspended(false);
                break;
            case SessionSwitchReason.ConsoleDisconnect:
                SetLocked(true);
                break;
            case SessionSwitchReason.ConsoleConnect:
                SetLocked(false);
                break;
            default:
                break;
        }
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                SetSuspended(true);
                break;
            case PowerModes.Resume:
                SetSuspended(false);
                break;
            default:
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }
}
