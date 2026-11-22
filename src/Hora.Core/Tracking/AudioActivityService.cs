using System.Runtime.InteropServices;

namespace Hora.Core.Tracking;

public interface IAudioActivityService
{
    /// <summary>True when any output audio session is actively playing.</summary>
    bool IsAudioPlaying();

    /// <summary>True when the process with this id owns a playing output session.</summary>
    bool IsAudioPlayingForProcess(int processId);
}

/// <summary>
/// Uses the Windows audio session meter to decide whether media is playing.
/// This keeps film, game, and meeting time counted even when the user is still.
/// </summary>
public sealed class AudioActivityService : IAudioActivityService
{
    private static readonly Guid ClsidMmDeviceEnumerator = new("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IidMmDeviceEnumerator = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

    public bool IsAudioPlaying() => IsAudioPlayingCore(null);

    public bool IsAudioPlayingForProcess(int processId) =>
        processId > 0 && IsAudioPlayingCore((uint)processId);

    private static bool IsAudioPlayingCore(uint? processId)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            var type = Type.GetTypeFromCLSID(ClsidMmDeviceEnumerator);
            if (type is null)
            {
                return false;
            }

            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(type)!;
            if (enumerator.GetDefaultAudioEndpoint(0, 0, out device) != 0 || device is null)
            {
                return false;
            }

            var managerId = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref managerId, 23, IntPtr.Zero, out var managerObject) != 0 || managerObject is null)
            {
                return false;
            }

            manager = managerObject as IAudioSessionManager2;
            if (manager is null)
            {
                _ = Marshal.ReleaseComObject(managerObject);
                return false;
            }
            if (manager.GetSessionEnumerator(out sessions) != 0 || sessions is null)
            {
                return false;
            }

            _ = sessions.GetCount(out var count);
            for (var index = 0; index < count; index++)
            {
                if (sessions.GetSession(index, out var control) != 0 || control is null)
                {
                    continue;
                }

                try
                {
                    // The fullscreen and meeting heuristics must only credit audio
                    // that belongs to the foreground application; a Spotify session
                    // must not keep a still user active in another app.
                    if (processId is { } wanted)
                    {
                        if (control is not IAudioSessionControl2 owner
                            || owner.GetProcessId(out var sessionProcess) != 0
                            || sessionProcess != wanted)
                        {
                            continue;
                        }
                    }

                    if (control is IAudioMeterInformation meter
                        && meter.GetPeakValue(out var peak) == 0
                        && peak > 0.0005f)
                    {
                        return true;
                    }
                }
                finally
                {
                    _ = Marshal.ReleaseComObject(control);
                }
            }

            return false;
        }
        catch (Exception exception) when (exception is COMException
                                              or InvalidCastException
                                              or NotSupportedException
                                              or InvalidOleVariantTypeException
                                              or ArgumentException)
        {
            // Audio is an optional signal. A broken audio stack must never stop
            // screen time tracking or surface as a crash.
            return false;
        }
        finally
        {
            if (sessions is not null)
            {
                _ = Marshal.ReleaseComObject(sessions);
            }

            if (manager is not null)
            {
                _ = Marshal.ReleaseComObject(manager);
            }

            if (device is not null)
            {
                _ = Marshal.ReleaseComObject(device);
            }

            if (enumerator is not null)
            {
                _ = Marshal.ReleaseComObject(enumerator);
            }
        }
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);

        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice? device);

        int GetDevice(string id, out IMMDevice? device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        // The activated object is an interface pointer, not a VARIANT. Without
        // IUnknown marshalling the CLR reads the result as a VARIANT and throws
        // InvalidOleVariantTypeException on every call.
        int Activate(
            ref Guid iid,
            uint classContext,
            IntPtr activationParams,
            [MarshalAs(UnmanagedType.IUnknown)] out object? instance);

        int OpenPropertyStore(int access, out IntPtr store);

        int GetId([MarshalAs(UnmanagedType.LPWStr)] out string? id);

        int GetState(out int state);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager methods come first in the vtable; omitting them
        // would shift GetSessionEnumerator and corrupt the call.
        int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out IntPtr sessionControl);

        int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out IntPtr volume);

        int GetSessionEnumerator(out IAudioSessionEnumerator sessions);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        int GetCount(out int count);

        int GetSession(int index, [MarshalAs(UnmanagedType.IUnknown)] out object? session);
    }

    [ComImport]
    [Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioMeterInformation
    {
        int GetPeakValue(out float peak);
    }

    [ComImport]
    [Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl members occupy the first eight slots.
        int GetState(out int state);

        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? name);

        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string? path);

        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        int GetGroupingParam(out Guid groupingParam);

        int SetGroupingParam(ref Guid groupingParam, ref Guid eventContext);

        int RegisterAudioSessionNotification(IntPtr client);

        int UnregisterAudioSessionNotification(IntPtr client);

        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);

        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string? id);

        int GetProcessId(out uint processId);

        int IsSystemSoundsSession();

        int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }
}
