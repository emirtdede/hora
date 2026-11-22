namespace Hora.Core.Aggregation;

/// <summary>One aggregated slice of usage.</summary>
public sealed record UsageSlice(string Key, string Label, long Seconds, double Percent, string Color);

/// <summary>Groups many rows into a fixed-size chart, folding the tail into "Other".</summary>
public static class UsageAggregator
{
    public static IReadOnlyList<UsageSlice> Group(
        IEnumerable<(string Key, string Label, long Seconds)> rows,
        IReadOnlyList<string> palette,
        int maxSlices = 7,
        string otherLabel = "Other")
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Count == 0)
        {
            throw new ArgumentException("At least one palette color is required.", nameof(palette));
        }

        var ordered = rows
            .Where(r => r.Seconds > 0)
            .OrderByDescending(r => r.Seconds)
            .ThenBy(r => r.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var total = ordered.Sum(r => r.Seconds);
        if (total <= 0)
        {
            return Array.Empty<UsageSlice>();
        }

        var keep = Math.Max(1, maxSlices);
        var slices = new List<UsageSlice>(keep + 1);
        var index = 0;
        foreach (var row in ordered.Take(keep))
        {
            slices.Add(new UsageSlice(
                row.Key,
                row.Label,
                row.Seconds,
                row.Seconds * 100.0 / total,
                palette[index % palette.Count]));
            index++;
        }

        var rest = ordered.Skip(keep).Sum(r => r.Seconds);
        if (rest > 0)
        {
            slices.Add(new UsageSlice(
                "__other__",
                otherLabel,
                rest,
                rest * 100.0 / total,
                palette[index % palette.Count]));
        }

        return slices;
    }

    /// <summary>
    /// Product names are proper nouns and stay unchanged in every language. System
    /// buckets are excluded before this method is called, so they have no label here.
    /// </summary>
    public static string FriendlyName(string exe) => exe switch
    {
        "chrome" => "Google Chrome",
        "msedge" => "Microsoft Edge",
        "firefox" => "Mozilla Firefox",
        "brave" => "Brave",
        "opera" => "Opera",
        "vivaldi" => "Vivaldi",
        "code" => "Visual Studio Code",
        "devenv" => "Visual Studio",
        "explorer" => "File Explorer",
        "snippingtool" => "Snipping Tool",
        "notepad" => "Notepad",
        "winword" => "Microsoft Word",
        "excel" => "Microsoft Excel",
        "powerpnt" => "Microsoft PowerPoint",
        "outlook" => "Microsoft Outlook",
        "teams" => "Microsoft Teams",
        "ms-teams" => "Microsoft Teams",
        "slack" => "Slack",
        "discord" => "Discord",
        "spotify" => "Spotify",
        "vlc" => "VLC",
        "photoshop" => "Adobe Photoshop",
        "illustrator" => "Adobe Illustrator",
        "figma" => "Figma",
        "windowsterminal" => "Windows Terminal",
        "powershell" => "PowerShell",
        "cmd" => "Command Prompt",
        _ => exe.Length > 0
            ? char.ToUpperInvariant(exe[0]) + exe[1..]
            : exe,
    };
}
