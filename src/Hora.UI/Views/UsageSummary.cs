using Hora.Core.Aggregation;
using Hora.Storage.Models;

namespace Hora.UI.Views;

/// <summary>One application rollup for a day or range.</summary>
public sealed record AppUsage(string Exe, string Label, long Seconds, int Domains);

/// <summary>One domain rollup.</summary>
public sealed record DomainUsage(string Domain, long Seconds);

/// <summary>Pure aggregation helpers shared by the dashboard and history pages.</summary>
public static class UsageSummary
{
    private static readonly HashSet<string> SpecialBuckets =
        new(SystemBuckets.Names, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<AppUsage> ByApplication(IEnumerable<UsageRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .Where(r => r.Seconds > 0 && !string.IsNullOrWhiteSpace(r.Exe))
            .GroupBy(r => r.Exe, StringComparer.OrdinalIgnoreCase)
            .Select(group => new AppUsage(
                group.Key,
                UsageAggregator.FriendlyName(group.Key),
                group.Sum(r => r.Seconds),
                group.Count(r => !string.IsNullOrWhiteSpace(r.Domain))))
            .OrderByDescending(row => row.Seconds)
            .ThenBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<AppUsage> TrackedApplications(IEnumerable<UsageRow> rows) =>
        ByApplication(rows).Where(row => !SpecialBuckets.Contains(row.Exe)).ToArray();

    public static IReadOnlyList<DomainUsage> ByDomain(IEnumerable<UsageRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .Where(r => r.Seconds > 0 && !string.IsNullOrWhiteSpace(r.Domain))
            .GroupBy(r => r.Domain, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DomainUsage(group.Key, group.Sum(r => r.Seconds)))
            .OrderByDescending(row => row.Seconds)
            .ThenBy(row => row.Domain, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static long Total(IEnumerable<UsageRow> rows) =>
        rows.Where(r => r.Seconds > 0 && !SpecialBuckets.Contains(r.Exe)).Sum(r => r.Seconds);

    public static long IdleSeconds(IEnumerable<UsageRow> rows) =>
        rows.Where(r => r.Seconds > 0 && string.Equals(r.Exe, "idle", StringComparison.OrdinalIgnoreCase))
            .Sum(r => r.Seconds);

    public static string DateKey(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static DateOnly ParseDateKey(string value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : DateOnly.FromDateTime(DateTime.Today);
}
