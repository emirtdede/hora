# Tracking Model

## Loop

`TrackingEngine` runs a one-second dispatcher timer while tracking is active.
Each tick samples:

1. The foreground window: process name and title (`ForegroundService`).
2. Idle time from `GetLastInputInfo` (`IdleService`).
3. Whole-screen fullscreen state (`FullscreenService`).
4. Audio activity, used as a media signal (`AudioActivityService`).
5. Session state: lock, unlock, suspend, resume (`SessionStateService`).
6. Meeting detection derived from the executable plus audio (`MeetingService`).

## Decision priority

`TrackingDecider.Decide` is pure and takes every signal as an argument, so the
order is testable without Win32:

| Priority | Condition | Bucket |
|---|---|---|
| 1 | Suspended | `suspended` |
| 2 | Workstation locked | `locked` |
| 3 | Meeting detected and a valid foreground window | meeting bucket, executable kept |
| 4 | Idle at or above 60 s | `idle` |
| 5 | Otherwise | `active`, executable and domain kept |

The invariant is one logical owner per second. A second is never counted twice,
and a system bucket never mixes with an application bucket.

## Domains

`DomainExtractor` only runs for known browsers. It reads the window title,
extracts a host-like token, strips `www.`, and keeps the registrable label plus
a known multi-part suffix (`co.uk`, `com.br`, and so on). When the title carries
no domain, the domain is empty and the second is attributed to the application
only.

## Privacy filter

`PrivacyFilter` runs before a title leaves the sampler. Password managers,
banking, payment, medical, and payroll surfaces are detected by executable and
by title keyword. Card-number patterns are detected by shape. A sensitive title
is replaced with a neutral placeholder; the executable and the duration are
still recorded so the user keeps an accurate total.

## Aggregation

`UsageAggregator` groups labelled rows into a bounded chart: the largest slices
stay, and the tail is folded into one `Other` slice. Percentages are computed
from the full total, never from the visible subset, so the chart always sums to
100.

`UsageSummary` in the UI is the one place that classifies system buckets, so
"total active time" means the same thing on the dashboard, history, and status
bar.

## Flush policy

Seconds accumulate in a dictionary keyed by `(exe, domain)`. The flush writes the
whole batch in one transaction and clears it only after a successful commit, so
a crash re-writes at most the current batch. `PendingActiveSeconds` exposes the
unflushed activity so the dashboard can include live seconds without waiting for
the next flush.
