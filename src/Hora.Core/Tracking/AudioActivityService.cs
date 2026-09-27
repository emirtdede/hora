using System.IO;
using Hora.Core.Logging;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace Hora.Core.Tracking;

public interface IAudioActivityService
{
    bool IsRenderActive();
    bool IsCaptureActive();
    float SampleRenderPeak();
}

public sealed class AudioActivityService : IAudioActivityService, IDisposable
{
    private readonly IHoraLogger _log;
    private readonly MMDeviceEnumerator _enumerator = new();
    private bool _disposed;

    public AudioActivityService(IHoraLogger log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public bool IsRenderActive()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            var sessions = device.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].State == AudioSessionState.AudioSessionStateActive)
                {
                    return true;
                }
            }

            return SampleRenderPeak() > 0.02f;
        }
        catch (Exception ex)
        {
            _log.Warn($"Render ses durumu okunamadı: {ex.Message}");
            return false;
        }
    }

    public bool IsCaptureActive()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
            var sessions = device.AudioSessionManager.Sessions;
            for (var i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].State == AudioSessionState.AudioSessionStateActive)
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            _log.Warn($"Mikrofon durumu okunamadı: {ex.Message}");
            return false;
        }
    }

    public float SampleRenderPeak()
    {
        try
        {
            using var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Console);
            return device.AudioMeterInformation.MasterPeakValue;
        }
        catch
        {
            return 0f;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _enumerator.Dispose();
    }
}
