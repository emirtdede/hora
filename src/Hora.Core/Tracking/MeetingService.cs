namespace Hora.Core.Tracking;

public interface IMeetingService
{
    bool IsMeetingActive(string exeName, string domain);
}

public sealed class MeetingService : IMeetingService
{
    private static readonly HashSet<string> MeetingExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "teams", "ms-teams", "zoom", "zoom meetings", "webex", "webexmta", "skype", "discord", "slack", "meet"
    };

    private static readonly HashSet<string> MeetingDomain = new(StringComparer.OrdinalIgnoreCase)
    {
        "meet.google.com", "teams.microsoft.com", "teams.live.com", "zoom.us", "webex.com", "meet.jit.si"
    };

    private readonly IAudioActivityService _audio;

    public MeetingService(IAudioActivityService audio)
    {
        _audio = audio ?? throw new ArgumentNullException(nameof(audio));
    }

    public bool IsMeetingActive(string exeName, string domain)
    {
        var known = MeetingExe.Contains(exeName) || MeetingDomain.Contains(domain);
        if (!known)
        {
            return false;
        }

        return _audio.IsRenderActive() || _audio.IsCaptureActive();
    }
}
