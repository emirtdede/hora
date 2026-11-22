using Hora.Core.Aggregation;
using Hora.Core.Browser;
using Hora.Core.Logging;
using Hora.Core.Privacy;
using Hora.Core.Tracking;
using Hora.Storage.Db;

namespace Hora.UI.Tracking;

/// <summary>
/// Samples the foreground window once per second, batches seconds in memory,
/// and flushes them to SQLite on demand. Only durations leave the sampler.
/// </summary>
public sealed class TrackingEngine : IDisposable
{
    private readonly IForegroundService _foreground;
    private readonly IIdleService _idle;
    private readonly IFullscreenService _fullscreen;
    private readonly IAudioActivityService _audio;
    private readonly ISessionStateService _session;
    private readonly IMeetingService _meeting;
    private readonly IDomainExtractor _domains;
    private readonly IPrivacyFilter _privacy;
    private readonly UsageRepository _repository;
    private readonly IHoraLogger _log;
    private readonly Dictionary<(string Date, string Exe, string Domain), long> _pending = new();
    private readonly object _sync = new();
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
        UsageRepository repository,
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
        _repository = repository;
        _log = log;
    }

    public bool IsPaused { get; set; }

    public string CurrentExe { get; private set; } = string.Empty;

    public string CurrentDomain { get; private set; } = string.Empty;

    public SampleKind CurrentKind { get; private set; } = SampleKind.Active;

    public long PendingSeconds
    {
        get
        {
            lock (_sync)
            {
                return _pending.Values.Sum();
            }
        }
    }

    /// <summary>Buffered seconds for real applications only, excluding idle, lock, and sleep.</summary>
    public long PendingActiveSeconds
    {
        get
        {
            lock (_sync)
            {
                return _pending.Where(pair => !IsSystemBucket(pair.Key.Exe)).Sum(pair => pair.Value);
            }
        }
    }

    private static bool IsSystemBucket(string exe) => SystemBuckets.IsSystem(exe);

    private static string Today() =>
        DateTime.Now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Runs one sample. Must be called once per second.</summary>
    public void Tick()
    {
        if (_disposed || IsPaused)
        {
            return;
        }

        var sample = _foreground.Sample();
        var idleSeconds = _idle.IdleSeconds();
        var locked = _session.IsLocked();
        var suspended = _session.IsSuspended();
        var meeting = _meeting.IsMeetingActive(sample.Exe, sample.ProcessId);

        // A fullscreen app with its own audio keeps counting even when the user is still.
        if (!meeting && _fullscreen.IsFullscreen() && sample.Valid
            && _audio.IsAudioPlayingForProcess(sample.ProcessId))
        {
            meeting = true;
        }

        var decision = TrackingDecider.Decide(sample, idleSeconds, locked, suspended, meeting);
        var domain = string.Empty;
        if (decision.Kind is SampleKind.Active or SampleKind.Meeting)
        {
            // Sensitive titles are dropped before any domain is derived.
            var safeTitle = _privacy.SanitizeTitle(sample.Title, sample.Exe);
            if (!string.IsNullOrEmpty(safeTitle))
            {
                domain = _domains.Extract(safeTitle, sample.Exe) ?? string.Empty;
            }
        }

        CurrentExe = decision.Kind is SampleKind.Idle or SampleKind.Locked or SampleKind.Suspended
            ? decision.Exe
            : sample.Exe;
        CurrentDomain = domain;
        CurrentKind = decision.Kind;

        // The day is captured per sample, so seconds recorded before midnight and
        // flushed after it still land on the correct date.
        var key = (Today(), decision.Kind is SampleKind.Idle or SampleKind.Locked or SampleKind.Suspended
            ? decision.Exe
            : sample.Exe, domain);
        if (string.IsNullOrWhiteSpace(key.Item2))
        {
            return;
        }

        lock (_sync)
        {
            _pending[key] = _pending.TryGetValue(key, out var current) ? current + 1 : 1;
        }
    }

    /// <summary>Writes buffered seconds. Safe to call concurrently.</summary>
    public async Task FlushAsync(CancellationToken ct = default)
    {
        Dictionary<(string Date, string Exe, string Domain), long> snapshot;
        lock (_sync)
        {
            if (_pending.Count == 0)
            {
                return;
            }

            snapshot = new Dictionary<(string Date, string Exe, string Domain), long>(_pending);
            _pending.Clear();
        }

        try
        {
            await _repository.AddBatchAsync(
                snapshot.Select(pair => new Hora.Storage.Models.UsageRow(
                        pair.Key.Date, pair.Key.Exe, pair.Key.Domain, pair.Value)),
                ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Restore(snapshot);
            throw;
        }
        catch (Exception exception)
        {
            _log.Error("Flush failed; seconds are kept for the next attempt.", exception);
            Restore(snapshot);
        }
    }

    /// <summary>Puts a snapshot back into the buffer after a failed write.</summary>
    private void Restore(Dictionary<(string Date, string Exe, string Domain), long> snapshot)
    {
        lock (_sync)
        {
            foreach (var (key, seconds) in snapshot)
            {
                _pending[key] = _pending.TryGetValue(key, out var current) ? current + seconds : seconds;
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
