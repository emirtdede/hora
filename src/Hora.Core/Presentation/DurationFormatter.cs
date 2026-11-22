using System.Globalization;

namespace Hora.Core.Presentation;

/// <summary>
/// Formats durations in a compact way. The hour, minute, and second suffixes are
/// measurement symbols ("h", "m", "s"), not prose, so they stay invariant in every
/// language. Prose around a duration comes from the catalog.
/// </summary>
public static class DurationFormatter
{
    public static string Format(long totalSeconds)
    {
        if (totalSeconds <= 0)
        {
            return "0m";
        }

        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        if (hours >= 1)
        {
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }

        if (minutes >= 1)
        {
            return $"{minutes}m";
        }

        return "<1m";
    }

    public static string FormatLong(long totalSeconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        if (span.TotalHours >= 1)
        {
            return string.Create(CultureInfo.CurrentCulture,
                $"{(int)span.TotalHours}h {span.Minutes:00}m {span.Seconds:00}s");
        }

        return string.Create(CultureInfo.CurrentCulture, $"{span.Minutes}m {span.Seconds:00}s");
    }

    /// <summary>Converts a duration into clock text such as 2:14 or 1:02:14.</summary>
    public static string FormatClock(long totalSeconds)
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, totalSeconds));
        return span.TotalHours >= 1
            ? string.Create(CultureInfo.InvariantCulture, $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{span.Minutes}:{span.Seconds:00}");
    }

    public static string FormatDay(DateOnly date, string languageId)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(languageId switch
            {
                "zh-tw" => "zh-TW",
                "fil" => "fil-PH",
                "no" => "nb-NO",
                _ => languageId,
            });
            return date.ToDateTime(TimeOnly.MinValue).ToString("ddd, d MMM", culture);
        }
        catch (CultureNotFoundException)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
    }
}
