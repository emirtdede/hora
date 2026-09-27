namespace Hora.Core.Aggregation;

public sealed class UsageAggregator
{
    private readonly Dictionary<(string Date, string Exe, string Domain), long> _pending = new();
    private readonly object _gate = new();

    public void Add(string date, string exe, string domain, long seconds)
    {
        if (string.IsNullOrWhiteSpace(date) || string.IsNullOrWhiteSpace(exe) || seconds <= 0)
        {
            return;
        }

        var key = (date, exe.ToLowerInvariant(), (domain ?? string.Empty).ToLowerInvariant());
        lock (_gate)
        {
            _pending[key] = _pending.TryGetValue(key, out var current) ? current + seconds : seconds;
        }
    }

    public IReadOnlyList<(string Date, string Exe, string Domain, long Seconds)> Drain()
    {
        lock (_gate)
        {
            var items = _pending.Select(kv => (kv.Key.Date, kv.Key.Exe, kv.Key.Domain, kv.Value)).ToList();
            _pending.Clear();
            return items;
        }
    }

    public int PendingCount
    {
        get
        {
            lock (_gate)
            {
                return _pending.Count;
            }
        }
    }
}
