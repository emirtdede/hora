using System.Text.Json;
using Hora.Storage.Backup;
using Xunit;

namespace Hora.Tests;

/// <summary>Each test gets a private folder under the system temp directory.</summary>
public sealed class BackupEngineTests : IDisposable
{
    private readonly string _root;
    private readonly DateTimeOffset _now = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);

    public BackupEngineTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "hora-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // A held file only slows cleanup on CI; the OS reclaims it later.
        }
    }

    private BackupEngine Engine(string? folder = null) => new(folder ?? _root, () => _now);

    private static BackupDocument Document(int usageRows = 2) => new()
    {
        App = "Hora",
        Version = "1.0.0",
        Provider = "OneDrive",
        ExportedAt = new DateTimeOffset(2026, 3, 20, 12, 0, 0, TimeSpan.Zero),
        Data = new BackupPayload
        {
            Settings = new Hora.Storage.Settings.SettingsSnapshot { Language = "tr", CompletedSetup = true },
            Usage = Enumerable.Range(0, usageRows)
                .Select(index => new UsageSnapshotRow("2026-03-20", "chrome", "example.com", 60 + index))
                .ToList(),
        },
    };

    [Fact]
    public async Task Write_creates_sync_previous_and_daily_files()
    {
        var engine = Engine();
        var result = await engine.WriteAsync(Document());

        Assert.Equal(BackupWriteStatus.Written, result.Status);
        Assert.True(File.Exists(engine.SyncPath));
        Assert.True(File.Exists(Path.Combine(_root, BackupEngine.BackupFileName(_now))));
        Assert.False(File.Exists(engine.PreviousPath));
    }

    [Fact]
    public async Task Second_write_rotates_the_previous_snapshot()
    {
        var engine = Engine();
        await engine.WriteAsync(Document(1));
        await engine.WriteAsync(Document(3));

        Assert.True(File.Exists(engine.PreviousPath));
        var previous = JsonDocument.Parse(await File.ReadAllTextAsync(engine.PreviousPath));
        var rows = previous.RootElement.GetProperty("data").GetProperty("usage").GetArrayLength();
        Assert.Equal(1, rows);
    }

    [Fact]
    public async Task Empty_document_never_overwrites_a_good_backup()
    {
        var engine = Engine();
        await engine.WriteAsync(Document());

        var empty = new BackupDocument { Data = new BackupPayload(), App = "Hora" };
        var result = await engine.WriteAsync(empty);

        Assert.Equal(BackupWriteStatus.SkippedEmpty, result.Status);
        var restored = await engine.ReadLatestAsync();
        Assert.NotNull(restored);
        Assert.Equal(2, restored!.Data.Usage.Count);
    }

    [Fact]
    public async Task Empty_document_is_written_when_no_backup_exists()
    {
        var engine = Engine();
        var result = await engine.WriteAsync(new BackupDocument { Data = new BackupPayload(), App = "Hora" });
        Assert.Equal(BackupWriteStatus.Written, result.Status);
    }

    [Fact]
    public async Task Corrupt_sync_file_falls_back_to_previous()
    {
        var engine = Engine();
        await engine.WriteAsync(Document(1));
        await engine.WriteAsync(Document(5));
        await File.WriteAllTextAsync(engine.SyncPath, "{ this is not json");

        var restored = await engine.ReadLatestAsync();
        Assert.NotNull(restored);
        Assert.True(restored!.Data.Usage.Count == 1);
    }

    [Fact]
    public async Task Corrupt_sync_and_previous_falls_back_to_daily_file()
    {
        var engine = Engine();
        await engine.WriteAsync(Document(4));
        await File.WriteAllTextAsync(engine.SyncPath, "broken");
        await File.WriteAllTextAsync(engine.PreviousPath, "broken");

        var restored = await engine.ReadLatestAsync();
        Assert.NotNull(restored);
        Assert.Equal(4, restored!.Data.Usage.Count);
    }

    [Fact]
    public async Task ReadLatest_returns_null_for_an_empty_folder() =>
        Assert.Null(await Engine().ReadLatestAsync());

    [Fact]
    public async Task Rotation_removes_files_older_than_fifteen_days()
    {
        var engine = Engine();
        Directory.CreateDirectory(_root);
        var stale = Path.Combine(_root, BackupEngine.BackupPrefix + "2026-02-01.json");
        var fresh = Path.Combine(_root, BackupEngine.BackupPrefix + "2026-03-18.json");
        await File.WriteAllTextAsync(stale, "{}");
        await File.WriteAllTextAsync(fresh, "{}");

        engine.Rotate();

        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(fresh));
    }

    [Fact]
    public async Task ListBackups_is_newest_first()
    {
        var engine = Engine();
        Directory.CreateDirectory(_root);
        foreach (var day in new[] { "2026-03-01", "2026-03-10", "2026-03-05" })
        {
            await File.WriteAllTextAsync(Path.Combine(_root, BackupEngine.BackupPrefix + day + ".json"), "{}");
        }

        var files = await engine.ListBackupsAsync();
        Assert.Equal(new[] { "2026-03-10", "2026-03-05", "2026-03-01" },
            files.Select(file => file.Date.ToString("yyyy-MM-dd")).ToArray());
    }

    [Fact]
    public void Relative_folder_is_rejected() =>
        Assert.Throws<ArgumentException>(() => new BackupEngine("relative\\folder"));

    [Fact]
    public void Unknown_app_payload_is_not_meaningful() =>
        Assert.False(BackupEngine.IsMeaningful(new BackupDocument { App = "Other", Data = new BackupPayload() }));

    [Fact]
    public void Backup_file_name_is_stable() =>
        Assert.Equal("hora-backup-2026-03-20.json", BackupEngine.BackupFileName(_now));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative")]
    [InlineData(@"..\escape")]
    public void Unsafe_paths_are_rejected(string? path) =>
        Assert.True(BackupLocationDetector.IsUnsafePath(path));

    [Fact]
    public void Absolute_local_path_is_safe() =>
        Assert.False(BackupLocationDetector.IsUnsafePath(_root));

    [Fact]
    public void DetectAll_always_exposes_local_and_custom()
    {
        var locations = BackupLocationDetector.DetectAll(null, _root);
        var local = Assert.Single(locations, location => location.Kind == BackupProviderKind.Local);
        Assert.Contains(locations, location => location.Kind == BackupProviderKind.Custom);
        Assert.False(string.IsNullOrWhiteSpace(local.Root));
        // A provider that is not installed has no folder; any provider that is
        // reported as detected must point at a usable root.
        Assert.All(locations, location =>
            Assert.Equal(location.Detected, !string.IsNullOrWhiteSpace(location.Root)));
    }

    [Fact]
    public async Task Concurrent_writes_publish_one_complete_document()
    {
        var engine = Engine();
        for (var iteration = 0; iteration < 25; iteration += 1)
        {
            await Task.WhenAll(
                engine.WriteAsync(Document(1)),
                engine.WriteAsync(Document(2)),
                engine.WriteAsync(Document(3)));

            // Read the sync file directly: falling back to a daily copy would hide
            // a truncated or interleaved write.
            var json = await File.ReadAllTextAsync(engine.SyncPath);
            var published = JsonSerializer.Deserialize<BackupDocument>(json, BackupEngine.JsonOptions);
            Assert.NotNull(published);
            Assert.NotNull(published!.Data);
            Assert.Contains(published.Data.Usage.Count, new[] { 1, 2, 3 });
            Assert.Equal(
                published.Data.Usage.Count,
                published.Data.Usage.Distinct().Count());
        }
    }

    [Fact]
    public async Task Settings_only_document_never_replaces_a_backup_with_usage()
    {
        var engine = Engine();
        await engine.WriteAsync(Document(2));

        var result = await engine.WriteAsync(Document(0));

        Assert.Equal(BackupWriteStatus.SkippedEmpty, result.Status);
        var latest = await engine.ReadLatestAsync();
        Assert.NotNull(latest);
        Assert.Equal(2, latest!.Data.Usage.Count);
    }

    [Theory]
    [InlineData("{\"app\":\"Hora\",\"data\":null}")]
    [InlineData("{\"app\":\"Hora\",\"data\":{\"usage\":null}}")]
    [InlineData("{\"app\":\"Hora\",\"data\":{\"settings\":null}}")]
    public async Task Structurally_incomplete_documents_are_ignored(string json)
    {
        var engine = Engine();
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(engine.SyncPath, json);

        Assert.Null(await engine.ReadLatestAsync());
    }

    [Fact]
    public void Backup_file_name_uses_the_invariant_calendar()
    {
        var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
        try
        {
            System.Threading.Thread.CurrentThread.CurrentCulture =
                new System.Globalization.CultureInfo("th-TH");
            Assert.Equal("hora-backup-2026-03-20.json", BackupEngine.BackupFileName(_now));
        }
        finally
        {
            System.Threading.Thread.CurrentThread.CurrentCulture = previous;
        }
    }
}
