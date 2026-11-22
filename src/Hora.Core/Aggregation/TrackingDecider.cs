using Hora.Core.Tracking;

namespace Hora.Core.Aggregation;

/// <summary>Outcome of a single one-second sample.</summary>
public enum SampleKind
{
    Active = 0,
    Idle = 1,
    Locked = 2,
    Suspended = 3,
    Meeting = 4,
}

/// <summary>The bucket a sample was attributed to.</summary>
public sealed record TrackedSample(string Exe, string Domain, SampleKind Kind);

/// <summary>
/// Pure decision logic for one sample. Kept side-effect free so it can be unit tested
/// without Win32 or a database.
/// </summary>
public static class TrackingDecider
{
    public const int IdleThresholdSeconds = 60;

    public static TrackedSample Decide(
        in ForegroundSample foreground,
        uint idleSeconds,
        bool locked,
        bool suspended,
        bool meetingActive)
    {
        if (suspended)
        {
            return new TrackedSample("suspended", string.Empty, SampleKind.Suspended);
        }

        if (locked)
        {
            return new TrackedSample("locked", string.Empty, SampleKind.Locked);
        }

        if (meetingActive && foreground.Valid)
        {
            return new TrackedSample(foreground.Exe, string.Empty, SampleKind.Meeting);
        }

        if (idleSeconds >= IdleThresholdSeconds)
        {
            return new TrackedSample("idle", string.Empty, SampleKind.Idle);
        }

        if (!foreground.Valid)
        {
            return new TrackedSample("idle", string.Empty, SampleKind.Idle);
        }

        return new TrackedSample(foreground.Exe, string.Empty, SampleKind.Active);
    }
}
