using Hora.Core.Aggregation;
using Hora.Core.Browser;
using Hora.Core.Logging;
using Hora.Core.Privacy;
using Hora.Core.Tracking;
using Hora.Storage.Db;
using Hora.UI.Tracking;

namespace Hora.Tests;

internal sealed class StubForeground : IForegroundService
{
    public ForegroundWindow Current { get; set; } = new("code", "Program.cs", 1234);
    public ForegroundWindow GetCurrent() => Current;
}

internal sealed class StubIdle : IIdleService
{
    public TimeSpan Inactivity { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan GetInactivity() => Inactivity;
    public bool IsIdle(TimeSpan threshold) => Inactivity >= threshold;
}

internal sealed class StubFullscreen : IFullscreenService
{
    public bool Value { get; set; }
    public bool IsForegroundFullscreen(IntPtr hwnd) => Value;
}

internal sealed class StubAudio : IAudioActivityService
{
    public bool RenderActive { get; set; }
    public bool CaptureActive { get; set; }
    public float Peak { get; set; }
    public bool IsRenderActive() => RenderActive;
    public bool IsCaptureActive() => CaptureActive;
    public float SampleRenderPeak() => Peak;
}

internal sealed class StubSession : ISessionStateService
{
    public bool IsLocked { get; set; }
    public event EventHandler<bool>? LockChanged;
    public void Raise(bool locked) => LockChanged?.Invoke(this, locked);
    public void Dispose() { }
}

internal sealed class StubDomains : IDomainExtractor
{
    public string? Value { get; set; }
    public string? TryGetDomain(IntPtr hwnd, string exeName) => Value;
    public string ExtractDomain(string? urlOrText) => urlOrText ?? string.Empty;
}

public sealed class TrackingEngineTests
{
    private static (TrackingEngine Engine, UsageRepository Repo) Create(
        StubForeground fg, StubIdle idle, StubFullscreen fs, StubAudio audio, StubSession session, string db)
    {
        var log = new FileHoraLogger(Path.Combine(Path.GetTempPath(), "hora-test.log"));
        var meeting = new MeetingService(audio);
        var repo = new UsageRepository(db);
        repo.InitializeAsync().GetAwaiter().GetResult();
        var engine = new TrackingEngine(fg, idle, fs, audio, session, meeting,
            new StubDomains(), new PrivacyFilter(), new UsageAggregator(), repo, log);
        return (engine, repo);
    }

    [Fact]
    public void Locked_WritesLockedRow()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var (engine, _) = Create(new StubForeground(), new StubIdle(), new StubFullscreen(),
            new StubAudio(), new StubSession { IsLocked = true }, db);
        using (engine)
        {
            var d = engine.Tick();
            Assert.Equal("Kilitli", d.Status);
            Assert.Equal("locked", d.Exe);
        }
    }

    [Fact]
    public void IdleBeyondThreshold_WritesIdleRow()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var (engine, _) = Create(new StubForeground(), new StubIdle { Inactivity = TimeSpan.FromSeconds(120) },
            new StubFullscreen(), new StubAudio(), new StubSession(), db);
        using (engine)
        {
            var d = engine.Tick();
            Assert.Equal("Boşta", d.Status);
            Assert.Equal("idle", d.Exe);
        }
    }

    [Fact]
    public async Task ActiveForeground_WritesAppRow()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var (engine, repo) = Create(new StubForeground(), new StubIdle(),
            new StubFullscreen(), new StubAudio(), new StubSession(), db);
        using (engine)
        {
            var d = engine.Tick();
            Assert.Equal("Canlı", d.Status);
            Assert.Equal("code", d.Exe);
            await engine.FlushAsync();
        }

        var rows = await repo.GetDayAsync(DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"));
        Assert.Contains(rows, r => r.Exe == "code" && r.Seconds >= 1);
    }

    [Fact]
    public async Task SensitiveDomain_StoredWithoutDomain()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var fg = new StubForeground { Current = new ForegroundWindow("chrome", "Banka", 99) };
        var log = new FileHoraLogger(Path.Combine(Path.GetTempPath(), "hora-test.log"));
        var audio = new StubAudio();
        var domains = new StubDomains { Value = "ziraatbank.com.tr" };
        var repo = new UsageRepository(db);
        await repo.InitializeAsync();
        using var engine = new TrackingEngine(fg, new StubIdle(), new StubFullscreen(), audio,
            new StubSession(), new MeetingService(audio), domains, new PrivacyFilter(),
            new UsageAggregator(), repo, log);

        var d = engine.Tick();
        Assert.Equal("Canlı", d.Status);
        Assert.Equal(string.Empty, d.Domain);
    }

    [Fact]
    public void FullscreenWithAudio_BypassesIdleAsFilm()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var fg = new StubForeground { Current = new ForegroundWindow("vlc", "Film", 42) };
        var (engine, _) = Create(fg, new StubIdle { Inactivity = TimeSpan.FromMinutes(5) },
            new StubFullscreen { Value = true },
            new StubAudio { RenderActive = true, Peak = 0.5f }, new StubSession(), db);
        using (engine)
        {
            var d = engine.Tick();
            Assert.Equal("Film", d.Status);
            Assert.Equal("vlc", d.Exe);
        }
    }

    [Fact]
    public void MeetingWithAudio_StaysMeetingDespiteIdle()
    {
        var db = Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.db");
        var fg = new StubForeground { Current = new ForegroundWindow("teams", "Toplantı", 77) };
        var (engine, _) = Create(fg, new StubIdle { Inactivity = TimeSpan.FromMinutes(10) },
            new StubFullscreen(), new StubAudio { RenderActive = true }, new StubSession(), db);
        using (engine)
        {
            var d = engine.Tick();
            Assert.Equal("Toplantı", d.Status);
        }
    }
}
