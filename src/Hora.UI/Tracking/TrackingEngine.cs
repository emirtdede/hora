using System.Runtime.InteropServices;
using Hora.Core.Aggregation;
using Hora.Core.Browser;
using Hora.Core.Logging;
using Hora.Core.Privacy;
using Hora.Core.Tracking;
using Hora.Storage.Db;

namespace Hora.UI.Tracking;

public sealed class TrackingDecision
{
    public string Status { get; set; } = "Canlı";
    public string Exe { get; set; } = "unknown";
    public string Domain { get; set; } = string.Empty;
    public string Date { get; set; } = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
}

public sealed class TrackingEngine : IDisposable
{
    private static readonly TimeSpan IdleThreshold = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MeetingCap = TimeSpan.FromHours(4);

    private readonly IForegroundService _foreground;
    private readonly IIdleService _idle;
    private readonly IFullscreenService _fullscreen;
    private readonly IAudioActivityService _audio;
    private readonly ISessionStateService _session;
    private readonly IMeetingService _meeting;
    private readonly IDomainExtractor _domains;
    private readonly IPrivacyFilter _privacy;
    private readonly UsageAggregator _aggregator;
    private readonly UsageRepository _repo;
    private readonly IHoraLogger _log;

    private DateTime _meetingStart = DateTime.MinValue;
    private int _peakHits;
    private bool _disposed;

    public TrackingEngine(
        IForegroundService foreground,
        IIdleService idle,
        IFullscreenService fullscreen,
        IAudioActivityService audio,
        ISessionStateService session,
        IMeetingService meeting,
        IDomainExtractor domains,
        IPrivacyFilter privacy,
        UsageAggregator aggregator,
        UsageRepository repo,
        IHoraLogger log)
    {
        _foreground = foreground;
        _idle = idle;
        _fullscreen = fullscreen;
        _audio = audio;
        _session = session;
        _meeting = meeting;
        _domains = domains;
        _privacy = privacy;
        _aggregator = aggregator;
        _repo = repo;
        _log = log;
    }

    public TrackingDecision Tick()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now).ToString("yyyy-MM-dd");
        var decision = new TrackingDecision { Date = date };

        try
        {
            if (_session.IsLocked)
            {
                decision.Status = "Kilitli";
                decision.Exe = "locked";
                _aggregator.Add(date, "locked", string.Empty, 1);
                return decision;
            }

            var fg = _foreground.GetCurrent();
            var hwnd = GetForegroundHandle();
            var idleFor = _idle.GetInactivity();

            var domain = _domains.TryGetDomain(hwnd, fg.ExeName) ?? string.Empty;
            if (string.IsNullOrEmpty(domain) && fg.WindowTitle.Length > 0)
            {
                domain = GuessDomainFromTitle(fg.WindowTitle);
            }

            // Gizlilik: pencere basliklari diske yazilmaz; hassas exe/domain icin
            // domain de saklanmaz, yalnizca exe adi kaydedilir.
            if (_privacy.IsSensitive(fg.ExeName, domain))
            {
                domain = string.Empty;
            }

            var isFullscreen = _fullscreen.IsForegroundFullscreen(hwnd);
            var renderActive = SampleRenderSmoothed();
            var mediaPlaying = isFullscreen && renderActive;
            var meetingActive = _meeting.IsMeetingActive(fg.ExeName, domain);

            if (meetingActive)
            {
                if (_meetingStart == DateTime.MinValue)
                {
                    _meetingStart = now;
                }

                if (now - _meetingStart > MeetingCap)
                {
                    decision.Status = "Boşta";
                    decision.Exe = "idle";
                    _aggregator.Add(date, "idle", string.Empty, 1);
                    return decision;
                }

                decision.Status = "Toplantı";
            }
            else
            {
                _meetingStart = DateTime.MinValue;
                if (mediaPlaying)
                {
                    decision.Status = "Film";
                }
                else if (idleFor >= IdleThreshold)
                {
                    decision.Status = "Boşta";
                    decision.Exe = "idle";
                    _aggregator.Add(date, "idle", string.Empty, 1);
                    return decision;
                }
                else
                {
                    decision.Status = "Canlı";
                }
            }

            decision.Exe = fg.ExeName;
            decision.Domain = domain;
            _aggregator.Add(date, fg.ExeName, domain, 1);
            return decision;
        }
        catch (Exception ex)
        {
            _log.Error("Tick hatası.", ex);
            decision.Status = "Canlı";
            return decision;
        }
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        var items = _aggregator.Drain();
        foreach (var (date, exe, domain, seconds) in items)
        {
            try
            {
                await _repo.AddSecondsAsync(date, exe, domain, seconds, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.Error($"Flush yazılamadı {exe}/{domain}.", ex);
            }
        }
    }

    private bool SampleRenderSmoothed()
    {
        try
        {
            var peak = _audio.SampleRenderPeak();
            if (peak > 0.02f)
            {
                _peakHits = Math.Min(_peakHits + 1, 3);
            }
            else
            {
                _peakHits = Math.Max(_peakHits - 1, 0);
            }

            if (_audio.IsRenderActive())
            {
                return true;
            }

            return _peakHits >= 1;
        }
        catch
        {
            return false;
        }
    }

    private static string GuessDomainFromTitle(string title)
    {
        var t = title.ToLowerInvariant();
        if (t.Contains("youtube")) return "youtube.com";
        if (t.Contains("netflix")) return "netflix.com";
        if (t.Contains("github")) return "github.com";
        if (t.Contains("gmail")) return "mail.google.com";
        return string.Empty;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private static IntPtr GetForegroundHandle()
    {
        try
        {
            return GetForegroundWindow();
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        (_audio as IDisposable)?.Dispose();
        _session.Dispose();
    }
}
