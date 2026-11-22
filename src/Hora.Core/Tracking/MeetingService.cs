namespace Hora.Core.Tracking;

public interface IMeetingService
{
    bool IsMeetingActive(string? exe, int processId);
}

/// <summary>
/// Recognizes meeting applications together with an active audio session.
/// Windows does not expose the microphone owner, so this is a best-effort signal.
/// </summary>
public sealed class MeetingService : IMeetingService
{
    private static readonly HashSet<string> MeetingApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "teams", "ms-teams", "zoom", "zoom.us", "webex", "webexmta", "gotomeeting", "skype",
        "slack", "discord", "meet", "slackhuddle", "bluejeans", "whereby", "jitsi",
    };

    private readonly IAudioActivityService _audio;

    public MeetingService(IAudioActivityService audio)
    {
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
    }

    public bool IsMeetingActive(string? exe, int processId)
    {
        if (string.IsNullOrWhiteSpace(exe) || processId <= 0)
        {
            return false;
        }

        var normalized = exe.Trim();
        if (!MeetingApps.Contains(normalized))
        {
            return false;
        }

        // Only the meeting application's own audio session counts. Any other
        // playback on the machine must not turn idle time into meeting time.
        return _audio.IsAudioPlayingForProcess(processId);
    }
}
