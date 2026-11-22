namespace Hora.Core.Aggregation;

/// <summary>
/// The executable keys that represent machine state rather than an application.
/// Every surface that computes an "active time" total must use this list, so a
/// locked or idle second can never be counted as screen time on one page and
/// excluded on another.
/// </summary>
public static class SystemBuckets
{
    public const string Idle = "idle";
    public const string Locked = "locked";
    public const string Suspended = "suspended";
    public const string Meeting = "meeting";

    private static readonly string[] All = { Idle, Locked, Suspended, Meeting };

    public static IReadOnlyList<string> Names => All;

    public static bool IsSystem(string? executable)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return false;
        }

        foreach (var name in All)
        {
            if (string.Equals(executable, name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
