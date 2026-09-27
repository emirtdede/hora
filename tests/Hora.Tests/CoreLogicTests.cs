using Hora.Core.Aggregation;
using Hora.Core.Browser;
using Hora.Core.Logging;
using Hora.Core.Privacy;
using Hora.Storage.Settings;

namespace Hora.Tests;

public sealed class CoreLogicTests
{
    private static DomainExtractor Extractor()
    {
        var log = new FileHoraLogger(Path.Combine(Path.GetTempPath(), "hora-test.log"));
        return new DomainExtractor(log);
    }

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=x", "youtube.com")]
    [InlineData("http://github.com/a/b", "github.com")]
    [InlineData("meet.google.com/abc-def", "meet.google.com")]
    [InlineData("", "")]
    public void ExtractDomain_ParsesHost(string input, string expected)
    {
        Assert.Equal(expected, Extractor().ExtractDomain(input));
    }

    [Fact]
    public void ExtractDomain_HandlesBareHost()
    {
        Assert.Equal("github.com", Extractor().ExtractDomain("github.com/a/b"));
    }

    [Theory]
    [InlineData("keepass", "", true)]
    [InlineData("chrome", "ziraatbank.com.tr", true)]
    [InlineData("chrome", "youtube.com", false)]
    [InlineData("code", "", false)]
    public void PrivacyFilter_MarksSensitive(string exe, string domain, bool expected)
    {
        var filter = new PrivacyFilter();
        Assert.Equal(expected, filter.IsSensitive(exe, domain));
    }

    [Fact]
    public void Aggregator_MergesSameKey()
    {
        var agg = new UsageAggregator();
        agg.Add("2026-09-27", "Chrome", "youtube.com", 1);
        agg.Add("2026-09-27", "chrome", "youtube.com", 2);
        agg.Add("2026-09-27", "code", "", 5);

        var drained = agg.Drain();
        Assert.Equal(2, drained.Count);
        Assert.Contains(drained, x => x.Exe == "chrome" && x.Seconds == 3);
        Assert.Equal(0, agg.PendingCount);
    }

    [Fact]
    public void Aggregator_IgnoresInvalid()
    {
        var agg = new UsageAggregator();
        agg.Add("", "chrome", "", 1);
        agg.Add("2026-09-27", "", "", 1);
        agg.Add("2026-09-27", "chrome", "", 0);
        Assert.Equal(0, agg.PendingCount);
    }

    [Fact]
    public void Settings_Sanitize_Clamps()
    {
        var store = new SettingsStore(Path.Combine(Path.GetTempPath(), $"hora-{Guid.NewGuid():N}.json"));
        var loaded = store.Load();
        Assert.Equal("Black", loaded.Theme);
        Assert.True(loaded.LaunchOnStartup);

        loaded.Width = 5000;
        loaded.Opacity = 99;
        loaded.Theme = "Neon";
        loaded.View = "Hatalı";
        store.Save(loaded);

        var again = store.Load();
        Assert.Equal(800, again.Width);
        Assert.Equal(1.0, again.Opacity);
        Assert.Equal("Black", again.Theme);
        Assert.Equal("Index", again.View);

        again.Theme = "Glass";
        store.Save(again);
        Assert.Equal("Glass", store.Load().Theme);
    }

    [Theory]
    [InlineData(0, "0 sn")]
    [InlineData(45, "45 sn")]
    [InlineData(300, "5 dk")]
    [InlineData(3900, "1 sa 5 dk")]
    public void DurationFormat_Works(long seconds, string expected)
    {
        Assert.Equal(expected, Hora.Core.Presentation.DurationFormatter.Format(seconds));
    }
}
