using System.Globalization;
using System.Text.RegularExpressions;
using Hora.Core.Aggregation;
using Hora.Core.Browser;
using Hora.Core.Localization;
using Hora.Core.Presentation;
using Hora.Core.Privacy;
using Hora.Core.Theming;
using Hora.Core.Tracking;
using Xunit;

namespace Hora.Tests;

public sealed class SystemBucketTests
{
    [Theory]
    [InlineData("idle")]
    [InlineData("IDLE")]
    [InlineData("locked")]
    [InlineData("suspended")]
    [InlineData("meeting")]
    public void System_states_are_recognized(string exe) => Assert.True(SystemBuckets.IsSystem(exe));

    [Theory]
    [InlineData("chrome.exe")]
    [InlineData("code")]
    [InlineData("")]
    [InlineData(null)]
    public void Applications_are_not_system_states(string? exe) => Assert.False(SystemBuckets.IsSystem(exe));

    [Fact]
    public void Names_cover_every_state_once() => Assert.Equal(
        SystemBuckets.Names.Count,
        SystemBuckets.Names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
}

public sealed class ThemeCatalogTests
{
    [Fact]
    public void Catalog_contains_twenty_eight_swatches() => Assert.Equal(28, ThemeCatalog.All.Count);

    [Fact]
    public void Catalog_ids_are_unique()
    {
        var ids = ThemeCatalog.All.Select(theme => theme.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Every_swatch_uses_valid_hex_colors()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            Assert.True(ThemeCatalog.IsValidHex(theme.Accent), theme.Id);
            Assert.True(ThemeCatalog.IsValidHex(theme.AccentSoft), theme.Id);
        }
    }

    [Theory]
    [InlineData("#7c8cf8", "#7C8CF8")]
    [InlineData("7C8CF8", "#7C8CF8")]
    [InlineData("  #AABBCC  ", "#AABBCC")]
    public void NormalizeHex_returns_canonical_form(string input, string expected) =>
        Assert.Equal(expected, ThemeCatalog.NormalizeHex(input));

    [Theory]
    [InlineData("#12345")]
    [InlineData("red")]
    [InlineData("#GGGGGG")]
    [InlineData("")]
    public void IsValidHex_rejects_malformed_values(string input) => Assert.False(ThemeCatalog.IsValidHex(input));

    [Fact]
    public void ContrastForeground_switches_on_luminance()
    {
        var onLight = ThemeCatalog.ContrastForeground("#FFFFFF");
        var onDark = ThemeCatalog.ContrastForeground("#101318");
        Assert.NotEqual(onLight, onDark);
        Assert.Equal("#FFFFFF", onDark);
        Assert.True(ThemeCatalog.ParseHex(onLight).R < 0x40);
    }

    [Fact]
    public void Unknown_theme_falls_back_to_default() =>
        Assert.Equal(ThemeCatalog.DefaultId, ThemeCatalog.Get("does-not-exist").Id);
}

public sealed class LanguageCatalogTests
{
    [Fact]
    public void Catalog_exposes_seventy_three_languages() => Assert.Equal(73, LanguageCatalog.All.Count);

    [Fact]
    public void Ids_are_unique() =>
        Assert.Equal(73, LanguageCatalog.All.Select(language => language.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());

    [Fact]
    public void Every_language_has_a_native_name() =>
        Assert.All(LanguageCatalog.All, language => Assert.False(string.IsNullOrWhiteSpace(language.NativeName)));

    [Theory]
    [InlineData("tr-TR", "tr")]
    [InlineData("zh-TW", "zh-tw")]
    [InlineData("pt-BR", "pt")]
    [InlineData("xx-YY", "en")]
    public void ResolveFromCulture_maps_unknown_to_english(string culture, string expected) =>
        Assert.Equal(expected, LanguageCatalog.ResolveFromCulture(culture));

    [Fact]
    public void Translator_falls_back_to_english_for_unknown_keys()
    {
        Translator.Instance.SetLanguage("en");
        Assert.False(string.IsNullOrWhiteSpace(Translator.Instance.Get("navDashboard")));
        Assert.Equal("missing.key", Translator.Instance.Get("missing.key"));
    }

    [Fact]
    public void Translator_formats_placeholders()
    {
        Translator.Instance.SetLanguage("en");
        var text = Translator.Instance.Format("wizardStep", 2, 4);
        Assert.Contains("2", text, StringComparison.Ordinal);
        Assert.Contains("4", text, StringComparison.Ordinal);
    }
}

public sealed class LocalizationGateTests
{
    private static readonly char[] Dashes = { '\u2012', '\u2013', '\u2014', '\u2015' };
    private static readonly Regex Placeholder = new(@"\{\d+\}", RegexOptions.Compiled);

    private static bool HasDash(string value) => value.IndexOfAny(Dashes) >= 0;

    private static bool HasEmoji(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            var code = rune.Value;
            if ((code >= 0x2700 && code <= 0x27BF)
                || (code >= 0x1F000 && code <= 0x1FAFF)
                || code == 0xFE0F)
            {
                return true;
            }
        }

        return false;
    }

    public static TheoryData<string> Languages()
    {
        var data = new TheoryData<string>();
        foreach (var language in LanguageCatalog.All)
        {
            data.Add(language.Id);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Languages))]
    public void Catalog_is_complete_and_clean(string id)
    {
        Translator.Instance.SetLanguage("en");
        var reference = Translator.Keys("en");
        var catalog = Translator.Instance.Catalog(id);

        Assert.Equal(reference.Count, catalog.Count);
        foreach (var key in reference)
        {
            Assert.True(catalog.ContainsKey(key), $"{id} is missing {key}");
            var value = catalog[key];
            Assert.False(string.IsNullOrWhiteSpace(value), $"{id}.{key} is empty");
            Assert.False(HasDash(value), $"{id}.{key} contains a dash character");
            Assert.False(HasEmoji(value), $"{id}.{key} contains an emoji");
            Assert.Equal(
                Placeholder.Matches(Translator.Instance.Catalog("en")[key]).Select(m => m.Value).OrderBy(v => v),
                Placeholder.Matches(value).Select(m => m.Value).OrderBy(v => v));
        }
    }

    [Fact]
    public void Every_theme_key_exists_in_every_catalog()
    {
        foreach (var theme in ThemeCatalog.All)
        {
            var key = "theme" + char.ToUpperInvariant(theme.Id[0]) + theme.Id[1..];
            foreach (var language in LanguageCatalog.All)
            {
                Translator.Instance.SetLanguage(language.Id);
                Assert.NotEqual(key, Translator.Instance.Get(key));
            }
        }
    }
}

public sealed class TrackingDeciderTests
{
    private static readonly ForegroundSample Chrome = new("chrome", "Example - Google Chrome", true);

    [Fact]
    public void Suspended_wins_over_every_other_signal() =>
        Assert.Equal(SampleKind.Suspended,
            TrackingDecider.Decide(Chrome, 0, locked: true, suspended: true, meetingActive: true).Kind);

    [Fact]
    public void Locked_wins_over_idle_and_meeting() =>
        Assert.Equal(SampleKind.Locked,
            TrackingDecider.Decide(Chrome, 120, locked: true, suspended: false, meetingActive: true).Kind);

    [Fact]
    public void Meeting_keeps_the_foreground_application() 
    {
        var sample = TrackingDecider.Decide(Chrome, 300, locked: false, suspended: false, meetingActive: true);
        Assert.Equal(SampleKind.Meeting, sample.Kind);
        Assert.Equal("chrome", sample.Exe);
    }

    [Fact]
    public void Idle_threshold_is_sixty_seconds()
    {
        Assert.Equal(SampleKind.Active,
            TrackingDecider.Decide(Chrome, 59, locked: false, suspended: false, meetingActive: false).Kind);
        Assert.Equal(SampleKind.Idle,
            TrackingDecider.Decide(Chrome, 60, locked: false, suspended: false, meetingActive: false).Kind);
    }

    [Fact]
    public void Invalid_foreground_is_idle() =>
        Assert.Equal(SampleKind.Idle,
            TrackingDecider.Decide(ForegroundSample.None, 0, locked: false, suspended: false, meetingActive: false).Kind);

    [Fact]
    public void Active_sample_reports_the_application() =>
        Assert.Equal("chrome",
            TrackingDecider.Decide(Chrome, 0, locked: false, suspended: false, meetingActive: false).Exe);
}

public sealed class SkipAggregationTests
{
    [Fact]
    public void Empty_input_returns_no_slices() =>
        Assert.Empty(UsageAggregator.Group(Array.Empty<(string, string, long)>(), new[] { "#000000" }));

    [Fact]
    public void Non_positive_rows_are_ignored() =>
        Assert.Empty(UsageAggregator.Group(new[] { ("a", "A", 0L), ("b", "B", -5L) }, new[] { "#000000" }));

    [Fact]
    public void Palette_is_required() =>
        Assert.Throws<ArgumentException>(() =>
            UsageAggregator.Group(new[] { ("a", "A", 5L) }, Array.Empty<string>()));

    [Fact]
    public void Tail_is_folded_into_other()
    {
        var rows = Enumerable.Range(0, 10).Select(index => ($"app{index}", $"App {index}", (long)(100 - index))).ToArray();
        var slices = UsageAggregator.Group(rows, new[] { "#111111", "#222222" }, 3, "Other");

        Assert.Equal(4, slices.Count);
        Assert.Equal("__other__", slices[^1].Key);
        Assert.Equal(rows.Skip(3).Sum(row => row.Item3), slices[^1].Seconds);
    }

    [Fact]
    public void Percentages_never_exceed_one_hundred()
    {
        var slices = UsageAggregator.Group(new[] { ("a", "A", 5L), ("b", "B", 5L) }, new[] { "#111111" });
        Assert.All(slices, slice => Assert.InRange(slice.Percent, 0, 100));
        Assert.Equal(100, slices.Sum(slice => slice.Percent), 3);
    }
}

public sealed class PrivacyFilterTests
{
    private readonly PrivacyFilter _filter = new();

    [Theory]
    [InlineData("keepass", "Personal vault")]
    [InlineData("chrome", "Online banking - Google Chrome")]
    [InlineData("msedge", "Sign in to your account")]
    public void Sensitive_surfaces_are_detected(string exe, string title) =>
        Assert.True(_filter.IsSensitive(title, exe));

    [Fact]
    public void Card_numbers_are_detected() =>
        Assert.True(_filter.IsSensitive("Order 4111 1111 1111 1111", "chrome"));

    [Fact]
    public void Ordinary_titles_are_not_sensitive() =>
        Assert.False(_filter.IsSensitive("Learn C# - Visual Studio Code", "code"));

    [Fact]
    public void Sensitive_titles_are_replaced() =>
        Assert.NotEqual("Online banking", _filter.SanitizeTitle("Online banking", "chrome"));
}

public sealed class DomainExtractorTests
{
    private readonly DomainExtractor _extractor = new();

    [Theory]
    [InlineData("instagram.com - Google Chrome", "instagram.com")]
    [InlineData("vercel.com | Dashboard", "vercel.com")]
    [InlineData("example.org - Mozilla Firefox", "example.org")]
    public void Extracts_registrable_domain(string title, string expected)
    {
        var exe = title.Contains("Firefox", StringComparison.Ordinal) ? "firefox" : "chrome";
        Assert.Equal(expected, _extractor.Extract(title, exe));
    }

    [Fact]
    public void Titles_without_a_domain_return_null() =>
        Assert.Null(_extractor.Extract("Instagram - Google Chrome", "chrome"));

    [Fact]
    public void Strips_www() =>
        Assert.Equal("example.com", _extractor.Extract("www.example.com - Google Chrome", "chrome"));

    [Fact]
    public void Keeps_multi_part_suffixes() =>
        Assert.Equal("example.co.uk", _extractor.Extract("example.co.uk - Google Chrome", "chrome"));

    [Fact]
    public void Non_browser_windows_return_null() =>
        Assert.Null(_extractor.Extract("example.com - Notepad", "notepad"));

    [Fact]
    public void Unknown_browsers_return_null() =>
        Assert.Null(_extractor.Extract("example.com - Some App", "someapp"));
}

public sealed class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0m")]
    [InlineData(-10, "0m")]
    [InlineData(59, "<1m")]
    [InlineData(60, "1m")]
    [InlineData(3600, "1h")]
    [InlineData(3660, "1h 1m")]
    public void Format_handles_boundaries(long seconds, string expected)
    {
        Translator.Instance.SetLanguage("en");
        Assert.Equal(expected, DurationFormatter.Format(seconds));
    }

    [Fact]
    public void FormatClock_switches_to_hours() =>
        Assert.Equal("1:02:14", DurationFormatter.FormatClock(3734));

    [Fact]
    public void FormatDay_uses_the_requested_culture()
    {
        var text = DurationFormatter.FormatDay(new DateOnly(2026, 1, 5), "en");
        Assert.Contains("Jan", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatUsesCurrentCultureSeparators()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Translator.Instance.SetLanguage("en");
            Assert.Equal("2m 05s", DurationFormatter.FormatLong(125));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
