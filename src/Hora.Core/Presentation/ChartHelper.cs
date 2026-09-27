namespace Hora.Core.Presentation;

public sealed record ChartSlice(string Label, string IconKey, long Seconds, double Percent, string Color);

public static class ChartHelper
{
    public const string OtherLabel = "Diğer";
    public const string OtherColor = "#8A90A6";

    public static IReadOnlyList<ChartSlice> GroupSmall(
        IReadOnlyList<(string Label, string IconKey, long Seconds)> rows,
        string[] palette,
        double minPercent = 2.0,
        int maxSlices = 7,
        double minOtherPercent = 1.0)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(palette);
        if (palette.Length == 0)
        {
            throw new ArgumentException("Palet boş olamaz.", nameof(palette));
        }

        var total = rows.Sum(r => r.Seconds);
        if (total <= 0 || rows.Count == 0)
        {
            return Array.Empty<ChartSlice>();
        }

        var main = new List<ChartSlice>();
        long otherSeconds = 0;
        var colorIndex = 0;

        foreach (var row in rows.OrderByDescending(r => r.Seconds))
        {
            var percent = row.Seconds * 100.0 / total;
            if (percent < minPercent || main.Count >= maxSlices)
            {
                otherSeconds += row.Seconds;
                continue;
            }

            main.Add(new ChartSlice(row.Label, row.IconKey, row.Seconds, percent, palette[colorIndex % palette.Length]));
            colorIndex++;
        }

        var otherPercent = total > 0 ? otherSeconds * 100.0 / total : 0;
        if (otherSeconds > 0 && otherPercent >= minOtherPercent)
        {
            main.Add(new ChartSlice(OtherLabel, string.Empty, otherSeconds, otherPercent, OtherColor));
        }

        return main;
    }
}
