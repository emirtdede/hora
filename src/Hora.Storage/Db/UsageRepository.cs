using Hora.Storage.Models;
using Microsoft.Data.Sqlite;

namespace Hora.Storage.Db;

public sealed class UsageRepository
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _writer = new(1, 1);

    public UsageRepository(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        _dbPath = dbPath;
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        await using var conn = Open();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS Events(
              Date TEXT NOT NULL,
              Exe TEXT NOT NULL,
              Domain TEXT NOT NULL DEFAULT '',
              Seconds INTEGER NOT NULL,
              PRIMARY KEY(Date, Exe, Domain)
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    public async Task AddSecondsAsync(string date, string exe, string domain, long seconds, CancellationToken ct = default)
    {
        await _writer.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await ExecuteWithRetryAsync(async () =>
            {
                await using var conn = Open();
                await conn.OpenAsync(ct).ConfigureAwait(false);
                await using var cmd = conn.CreateCommand();
                cmd.CommandText = """
                    INSERT INTO Events(Date, Exe, Domain, Seconds)
                    VALUES($d, $e, $m, $s)
                    ON CONFLICT(Date, Exe, Domain)
                    DO UPDATE SET Seconds = Seconds + excluded.Seconds;
                    """;
                cmd.Parameters.AddWithValue("$d", date);
                cmd.Parameters.AddWithValue("$e", exe);
                cmd.Parameters.AddWithValue("$m", domain ?? string.Empty);
                cmd.Parameters.AddWithValue("$s", seconds);
                await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task<IReadOnlyList<UsageRow>> GetDayAsync(string date, CancellationToken ct = default)
    {
        await using var conn = Open();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Exe, Domain, Seconds FROM Events WHERE Date = $d ORDER BY Seconds DESC;";
        cmd.Parameters.AddWithValue("$d", date);
        var rows = new List<UsageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new UsageRow(reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<UsageRow>> GetRangeAsync(string startDate, string endDate, CancellationToken ct = default)
    {
        await using var conn = Open();
        await conn.OpenAsync(ct).ConfigureAwait(false);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Exe, Domain, SUM(Seconds) FROM Events
            WHERE Date >= $s AND Date <= $e
            GROUP BY Exe, Domain ORDER BY SUM(Seconds) DESC;
            """;
        cmd.Parameters.AddWithValue("$s", startDate);
        cmd.Parameters.AddWithValue("$e", endDate);
        var rows = new List<UsageRow>();
        await using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new UsageRow(reader.GetString(0), reader.GetString(1), reader.GetInt64(2)));
        }

        return rows;
    }

    private SqliteConnection Open() => new($"Data Source={_dbPath}");

    private static async Task ExecuteWithRetryAsync(Func<Task> action, CancellationToken ct)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 5 && attempt < 2)
            {
                await Task.Delay(50 * (attempt + 1), ct).ConfigureAwait(false);
            }
        }
    }
}
