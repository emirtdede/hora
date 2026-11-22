using Hora.Storage.Models;
using Microsoft.Data.Sqlite;

namespace Hora.Storage.Db;

/// <summary>
/// SQLite persistence for usage aggregates. WAL mode keeps reads fast while a
/// background writer appends seconds once per flush.
/// </summary>
public sealed class UsageRepository
{
    private readonly string _dbPath;
    private readonly SemaphoreSlim _writer = new(1, 1);

    public UsageRepository(string dbPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbPath);
        _dbPath = dbPath;
    }

    public string DatabasePath => _dbPath;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await _writer.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;
                PRAGMA busy_timeout=4000;
                CREATE TABLE IF NOT EXISTS Events(
                  Date TEXT NOT NULL,
                  Exe TEXT NOT NULL,
                  Domain TEXT NOT NULL DEFAULT '',
                  Seconds INTEGER NOT NULL,
                  PRIMARY KEY(Date, Exe, Domain)
                );
                CREATE INDEX IF NOT EXISTS IX_Events_Date ON Events(Date);
                """;
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task AddSecondsAsync(string date, string exe, string domain, long seconds, CancellationToken ct = default)
    {
        if (seconds <= 0)
        {
            return;
        }

        await AddBatchAsync(new[] { new UsageRow(date, exe, domain ?? string.Empty, seconds) }, ct)
            .ConfigureAwait(false);
    }

    /// <summary>Writes a whole flush batch in one transaction so a failure leaves no partial rows.</summary>
    public async Task AddBatchAsync(IEnumerable<UsageRow> rows, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var list = rows.Where(row => row.Seconds > 0 && !string.IsNullOrWhiteSpace(row.Exe)).ToArray();
        if (list.Length == 0)
        {
            return;
        }

        await _writer.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO Events(Date, Exe, Domain, Seconds)
                VALUES($date, $exe, $domain, $seconds)
                ON CONFLICT(Date, Exe, Domain)
                DO UPDATE SET Seconds = Seconds + excluded.Seconds;
                """;
            var date = command.Parameters.Add("$date", SqliteType.Text);
            var exe = command.Parameters.Add("$exe", SqliteType.Text);
            var domain = command.Parameters.Add("$domain", SqliteType.Text);
            var seconds = command.Parameters.Add("$seconds", SqliteType.Integer);
            foreach (var row in list)
            {
                date.Value = row.Date;
                exe.Value = row.Exe;
                domain.Value = row.Domain ?? string.Empty;
                seconds.Value = row.Seconds;
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writer.Release();
        }
    }

    public async Task<IReadOnlyList<UsageRow>> GetDayAsync(string date, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(date);
        await using var connection = Open();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Date, Exe, Domain, Seconds
            FROM Events
            WHERE Date = $date AND Seconds > 0
            ORDER BY Seconds DESC;
            """;
        command.Parameters.AddWithValue("$date", date);
        var rows = new List<UsageRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new UsageRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<UsageRow>> GetRangeAsync(string fromDate, string toDate, CancellationToken ct = default)
    {
        await using var connection = Open();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Date, Exe, Domain, Seconds
            FROM Events
            WHERE Date >= $from AND Date <= $to AND Seconds > 0
            ORDER BY Date ASC, Seconds DESC;
            """;
        command.Parameters.AddWithValue("$from", fromDate);
        command.Parameters.AddWithValue("$to", toDate);
        var rows = new List<UsageRow>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(new UsageRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetInt64(3)));
        }

        return rows;
    }

    public async Task<IReadOnlyList<string>> GetKnownExecutablesAsync(int limit = 200, CancellationToken ct = default)
    {
        await using var connection = Open();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Exe FROM Events
            WHERE Exe NOT IN ('idle', 'locked', 'suspended', 'meeting')
            GROUP BY Exe
            ORDER BY SUM(Seconds) DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 2000));
        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    public async Task<long> CountRowsAsync(CancellationToken ct = default)
    {
        await using var connection = Open();
        await connection.OpenAsync(ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Events;";
        var value = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return value is long count ? count : 0;
    }

    public async Task DeleteAllAsync(CancellationToken ct = default)
    {
        await _writer.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using (var delete = connection.CreateCommand())
            {
                delete.CommandText = "DELETE FROM Events;";
                await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            // VACUUM cannot run inside a transaction and fails while another
            // connection is reading. The delete is already committed, so a busy
            // database must not be reported as a failed reset.
            try
            {
                await using var vacuum = connection.CreateCommand();
                vacuum.CommandText = "VACUUM;";
                await vacuum.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException)
            {
                // The space is reclaimed on the next successful vacuum.
            }
        }
        finally
        {
            _writer.Release();
        }
    }

    /// <summary>Replaces all rows with a restored snapshot inside one transaction.</summary>
    public async Task ReplaceAllAsync(IEnumerable<UsageRow> rows, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rows);

        // Validate first so a malformed backup cannot abort the transaction part
        // way through, and merge duplicate keys instead of overwriting them.
        var merged = new Dictionary<(string Date, string Exe, string Domain), long>();
        foreach (var row in rows)
        {
            if (row.Seconds <= 0
                || string.IsNullOrWhiteSpace(row.Date)
                || string.IsNullOrWhiteSpace(row.Exe)
                || !DateOnly.TryParseExact(row.Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out _))
            {
                continue;
            }

            var key = (row.Date, row.Exe, row.Domain ?? string.Empty);
            merged[key] = merged.TryGetValue(key, out var current) ? current + row.Seconds : row.Seconds;
        }

        await _writer.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = Open();
            await connection.OpenAsync(ct).ConfigureAwait(false);
            await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)transaction;
                delete.CommandText = "DELETE FROM Events;";
                await delete.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }

            await using (var insert = connection.CreateCommand())
            {
                insert.Transaction = (SqliteTransaction)transaction;
                insert.CommandText = """
                    INSERT INTO Events(Date, Exe, Domain, Seconds)
                    VALUES($date, $exe, $domain, $seconds)
                    ON CONFLICT(Date, Exe, Domain)
                    DO UPDATE SET Seconds = excluded.Seconds;
                    """;
                var date = insert.Parameters.Add("$date", SqliteType.Text);
                var exe = insert.Parameters.Add("$exe", SqliteType.Text);
                var domain = insert.Parameters.Add("$domain", SqliteType.Text);
                var seconds = insert.Parameters.Add("$seconds", SqliteType.Integer);
                foreach (var ((rowDate, rowExe, rowDomain), value) in merged)
                {
                    date.Value = rowDate;
                    exe.Value = rowExe;
                    domain.Value = rowDomain;
                    seconds.Value = value;
                    await insert.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _writer.Release();
        }
    }

    private SqliteConnection Open() => new($"Data Source={_dbPath}");
}
