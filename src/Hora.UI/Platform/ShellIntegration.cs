using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Hora.Core.Localization;
using Hora.Core.Logging;
using Microsoft.Win32;

namespace Hora.UI.Platform;

/// <summary>Start with Windows support through the per-user Run key.</summary>
public static class AutostartHelper
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Hora";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(ValueName) is string value && value.Contains("Hora", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static void SetEnabled(bool enabled, bool silent = true)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var exe = Environment.ProcessPath;
                if (string.IsNullOrEmpty(exe))
                {
                    return;
                }

                key.SetValue(ValueName, silent ? $"\"{exe}\" --minimized" : $"\"{exe}\"", RegistryValueKind.String);
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // Registry failures never block the UI.
        }
    }
}

/// <summary>Registers a system-wide show/hide shortcut.</summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0x4852;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    private readonly IHoraLogger _log;
    private HwndSource? _source;
    private Action? _callback;
    private bool _registered;

    public HotkeyService(IHoraLogger log)
    {
        _log = log;
    }

    public event EventHandler? Pressed;

    public bool TryRegister(Window window, string? gesture)
    {
        if (window is null || string.IsNullOrWhiteSpace(gesture))
        {
            return false;
        }

        Unregister();
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        if (_source is null)
        {
            return false;
        }

        _callback ??= () => Pressed?.Invoke(this, EventArgs.Empty);
        _source.AddHook(WndProc);
        if (!TryParse(gesture, out var modifiers, out var key))
        {
            return false;
        }

        _registered = RegisterHotKey(_source.Handle, HotkeyId, modifiers, key);
        _log.Info(_registered ? "Global shortcut registered." : "Global shortcut unavailable.");
        return _registered;
    }

    public void Unregister()
    {
        if (_source is not null && _registered)
        {
            _ = UnregisterHotKey(_source.Handle, HotkeyId);
        }

        _registered = false;
        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    public static bool TryParse(string gesture, out uint modifiers, out uint key)
    {
        modifiers = 0;
        key = 0;
        var parts = gesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        foreach (var part in parts[..^1])
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "WIN":
                    modifiers |= ModWin;
                    break;
                default:
                    return false;
            }
        }

        var last = parts[^1].ToUpperInvariant();
        if (last.Length == 1 && char.IsAsciiLetterOrDigit(last[0]))
        {
            key = last[0];
            return true;
        }

        if (last.StartsWith('F') && int.TryParse(last[1..], out var f) && f is >= 1 and <= 24)
        {
            key = (uint)(0x70 + f - 1);
            return true;
        }

        key = last switch
        {
            "SPACE" => 0x20,
            "TAB" => 0x09,
            "HOME" => 0x24,
            "END" => 0x23,
            "INSERT" => 0x2D,
            "DELETE" => 0x2E,
            "PAUSE" => 0x13,
            _ => 0,
        };
        return key != 0;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            _callback?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}

/// <summary>Builds the taskbar jump list for quick actions.</summary>
public static class JumpListHelper
{
    public static void Apply(bool enabled, string exe, string iconPath)
    {
        if (!enabled || string.IsNullOrEmpty(exe))
        {
            return;
        }

        try
        {
            var list = System.Windows.Shell.JumpList.GetJumpList(Application.Current);
            if (list is null)
            {
                list = new System.Windows.Shell.JumpList();
                System.Windows.Shell.JumpList.SetJumpList(Application.Current, list);
            }

            list.JumpItems.Clear();
            list.JumpItems.Add(new System.Windows.Shell.JumpTask
            {
                Title = "Hora",
                ApplicationPath = exe,
                Arguments = string.Empty,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
                IconResourcePath = iconPath,
                Description = Translator.Instance.Get("jumpOpen"),
            });
            list.JumpItems.Add(new System.Windows.Shell.JumpTask
            {
                Title = Translator.Instance.Get("navSettings"),
                ApplicationPath = exe,
                Arguments = "--settings",
                WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
                IconResourcePath = iconPath,
                Description = Translator.Instance.Get("jumpOpenSettings"),
            });
            list.Apply();
        }
        catch (Exception exception) when (exception is InvalidOperationException or UnauthorizedAccessException or IOException)
        {
            // Jump lists are a convenience; failures are silent by design.
        }
    }
}

/// <summary>Watches the Windows app theme so the System mode can follow it live.</summary>
public sealed class SystemThemeWatcher : IDisposable
{
    private readonly Action<bool> _onChanged;
    private bool? _lastDark;

    public SystemThemeWatcher(Action<bool> onChanged)
    {
        _onChanged = onChanged;
        SystemEvents.UserPreferenceChanged += OnPreferenceChanged;
        _lastDark = Core.Theming.ThemeCatalog.IsDark(Core.Theming.ThemeMode.System);
    }

    private void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        var dark = Core.Theming.ThemeCatalog.IsDark(Core.Theming.ThemeMode.System);
        if (_lastDark == dark)
        {
            return;
        }

        _lastDark = dark;
        _onChanged(dark);
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
}

/// <summary>Plays the short notification sound selected in Windows.</summary>
public static class SoundHelper
{
    public static void PlayNotification()
    {
        try
        {
            System.Media.SystemSounds.Asterisk.Play();
        }
        catch (InvalidOperationException)
        {
            // No audio device is not an error worth reporting.
        }
    }
}
