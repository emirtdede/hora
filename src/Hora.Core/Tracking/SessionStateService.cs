using System.Diagnostics;
using Microsoft.Win32;

namespace Hora.Core.Tracking;

public interface ISessionStateService : IDisposable
{
    bool IsLocked { get; }
    event EventHandler<bool>? LockChanged;
}

public sealed class SessionStateService : ISessionStateService
{
    private bool _disposed;

    public SessionStateService()
    {
        SystemEvents.SessionSwitch += OnSessionSwitch;
        IsLocked = CheckLogonUiRunning();
    }

    public bool IsLocked { get; private set; }

    public event EventHandler<bool>? LockChanged;

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        if (e.Reason == SessionSwitchReason.SessionLock)
        {
            IsLocked = true;
            LockChanged?.Invoke(this, true);
        }
        else if (e.Reason == SessionSwitchReason.SessionUnlock)
        {
            IsLocked = false;
            LockChanged?.Invoke(this, false);
        }
    }

    public static bool CheckLogonUiRunning()
    {
        try
        {
            return Process.GetProcessesByName("LogonUI").Length > 0;
        }
        catch
        {
            return false;
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
    }
}
