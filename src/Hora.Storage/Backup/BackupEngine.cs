using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hora.Storage.Backup;

/// <summary>Identifies a storage provider that can host the backup folder.</summary>
public enum BackupProviderKind
{
    OneDrive = 0,
    GoogleDrive = 1,
    Dropbox = 2,
    ICloud = 3,
    Custom = 4,
    Local = 5,
}

/// <summary>A resolved backup root folder for one provider.</summary>
public sealed record BackupLocation(BackupProviderKind Kind, string Root, bool Detected, bool IsCloud);

/// <summary>Finds the local sync folders installed on this machine. No network or OAuth is involved.</summary>
public static class BackupLocationDetector
{
    public static string? DetectRoot(BackupProviderKind kind) => kind switch
    {
        BackupProviderKind.OneDrive => DetectOneDrive(),
        BackupProviderKind.GoogleDrive => DetectGoogleDrive(),
        BackupProviderKind.Dropbox => DetectDropbox(),
        BackupProviderKind.ICloud => DetectICloud(),
        _ => null,
    };

    public static IReadOnlyList<BackupLocation> DetectAll(string? customRoot, string fallbackRoot)
    {
        var list = new List<BackupLocation>();
        foreach (var kind in new[]
                 {
                     BackupProviderKind.OneDrive, BackupProviderKind.GoogleDrive, BackupProviderKind.Dropbox,
                     BackupProviderKind.ICloud,
                 })
        {
            var root = DetectRoot(kind);
            list.Add(new BackupLocation(kind, root ?? string.Empty, root is not null, root is not null));
        }

        var custom = string.IsNullOrWhiteSpace(customRoot) ? null : customRoot;
        list.Add(new BackupLocation(BackupProviderKind.Custom, custom ?? string.Empty, custom is not null, false));
        list.Add(new BackupLocation(BackupProviderKind.Local, fallbackRoot, true, false));
        return list;
    }

    private static string? DetectOneDrive()
    {
        foreach (var name in new[] { "OneDriveConsumer", "OneDrive", "OneDriveCommercial" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (IsUsableFolder(value))
            {
                return value!;
            }
        }

        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "OneDrive");
        return Directory.Exists(home) ? home : null;
    }

    private static string? DetectGoogleDrive()
    {
        var env = Environment.GetEnvironmentVariable("GoogleDrive");
        if (IsUsableFolder(env))
        {
            return env!;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        foreach (var candidate in new[] { "Google Drive", "My Drive", Path.Combine("Google Drive", "My Drive") })
        {
            var path = Path.Combine(home, candidate);
            if (Directory.Exists(path))
            {
                return path;
            }
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                var path = Path.Combine(drive.RootDirectory.FullName, "My Drive");
                if (Directory.Exists(path))
                {
                    return path;
                }
            }
            catch (IOException)
            {
                // A disconnected drive is not an error for detection.
            }
        }

        return null;
    }

    private static string? DetectDropbox()
    {
        var env = Environment.GetEnvironmentVariable("Dropbox");
        if (IsUsableFolder(env))
        {
            return env!;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidate = Path.Combine(home, "Dropbox");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        try
        {
            var info = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Dropbox", "info.json");
            if (!File.Exists(info))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(info));
            foreach (var property in new[] { "personal", "business" })
            {
                if (document.RootElement.TryGetProperty(property, out var entry)
                    && entry.TryGetProperty("path", out var path)
                    && IsUsableFolder(path.GetString()))
                {
                    return path.GetString();
                }
            }
        }
        catch (JsonException)
        {
            // A malformed client file only disables automatic detection.
        }
        catch (IOException)
        {
            // Reading the client file is best effort.
        }

        return null;
    }

    private static string? DetectICloud()
    {
        var env = Environment.GetEnvironmentVariable("iCloudDrive");
        if (IsUsableFolder(env))
        {
            return env!;
        }

        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "iCloudDrive");
        return Directory.Exists(home) ? home : null;
    }

    private static bool IsUsableFolder(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Directory.Exists(value) && !IsUnsafePath(value);

    /// <summary>Rejects relative, rooted device, and traversal paths before any file operation.</summary>
    public static bool IsUnsafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return true;
        }

        try
        {
            if (!Path.IsPathFullyQualified(path))
            {
                return true;
            }

            // Inspect the raw path: GetFullPath would resolve ".." away before a
            // check could see it.
            foreach (var segment in path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                if (segment == "..")
                {
                    return true;
                }
            }

            if (path.StartsWith(@"\\?\", StringComparison.Ordinal)
                || path.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return true;
            }

            _ = Path.GetFullPath(path);
            return false;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return true;
        }
    }

    public static void OpenFolder(string path)
    {
        if (IsUnsafePath(path) || !Directory.Exists(path))
        {
            throw new DirectoryNotFoundException(path);
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{path}\"",
            UseShellExecute = true,
        });
    }
}

/// <summary>Serialized backup payload. Versioned so future releases can migrate safely.</summary>
public sealed class BackupDocument
{
    public string App { get; set; } = "Hora";

    public string Version { get; set; } = "1.0.0";

    public string Provider { get; set; } = "OneDrive";

    public DateTimeOffset ExportedAt { get; set; } = DateTimeOffset.Now;

    public BackupPayload Data { get; set; } = new();

    [JsonIgnore]
    public int RecordCount => Data.Usage.Count + Data.Settings.SavedThemes.Count + 1;
}

/// <summary>The user data captured by a backup.</summary>
public sealed class BackupPayload
{
    public Settings.SettingsSnapshot Settings { get; set; } = new();

    public List<UsageSnapshotRow> Usage { get; set; } = new();

    public List<string> KnownApplications { get; set; } = new();
}

/// <summary>A single usage aggregate row. Domains stay optional.</summary>
public sealed record UsageSnapshotRow(string Date, string Exe, string Domain, long Seconds);

/// <summary>Result of a write attempt.</summary>
public enum BackupWriteStatus
{
    Written = 0,
    SkippedEmpty = 1,
    Failed = 2,
}

public sealed record BackupWriteResult(BackupWriteStatus Status, string? Path, string? Error)
{
    public static BackupWriteResult Ok(string path) => new(BackupWriteStatus.Written, path, null);
    public static BackupWriteResult Skipped() => new(BackupWriteStatus.SkippedEmpty, null, null);
    public static BackupWriteResult Fail(string error) => new(BackupWriteStatus.Failed, null, error);
}

/// <summary>A backup file on disk.</summary>
public sealed record BackupFileInfo(string Path, DateTimeOffset Date, long Length);

/// <summary>
/// Writes and reads versioned backups with atomic replacement, a previous snapshot,
/// a fifteen day daily rotation, and a corruption recovery chain.
/// </summary>
public sealed class BackupEngine
{
    public const string SyncFileName = "hora-sync.json";
    public const string PreviousFileName = "hora-previous.json";
    public const string BackupPrefix = "hora-backup-";
    public const int RetentionDays = 15;

    /// <summary>
    /// Shared options for the on-disk backup format. Tests and external readers
    /// must use the same options, otherwise camelCase files deserialize to an
    /// empty payload and look truncated.
    /// </summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly Func<DateTimeOffset> _clock;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public BackupEngine(string folder, Func<DateTimeOffset>? clock = null)
    {
        if (BackupLocationDetector.IsUnsafePath(folder))
        {
            throw new ArgumentException("A fully qualified folder path is required.", nameof(folder));
        }

        Folder = folder;
        _clock = clock ?? (() => DateTimeOffset.Now);
    }

    public string Folder { get; }

    public string SyncPath => Path.Combine(Folder, SyncFileName);

    public string PreviousPath => Path.Combine(Folder, PreviousFileName);

    public static string BackupFileName(DateTimeOffset date) =>
        $"{BackupPrefix}{date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.json";

    public async Task<BackupWriteResult> WriteAsync(BackupDocument document, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        // One writer at a time: the debounce timer, a manual sync, and the
        // shutdown flush can otherwise share a temp file and publish half a file.
        await _writeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(Folder);
            SweepTemporaryFiles();

            // A settings-only document must never replace a backup that carries
            // usage rows, or the last good snapshot would be lost.
            if (!HasUsage(document) && await HasUsableBackupAsync(ct).ConfigureAwait(false))
            {
                return BackupWriteResult.Skipped();
            }

            if (File.Exists(SyncPath) && await TryReadDocumentAsync(SyncPath, ct).ConfigureAwait(false) is not null)
            {
                await AtomicWriteAsync(PreviousPath, await File.ReadAllTextAsync(SyncPath, ct).ConfigureAwait(false), ct)
                    .ConfigureAwait(false);
            }

            var json = JsonSerializer.Serialize(document, JsonOptions);
            await AtomicWriteAsync(SyncPath, json, ct).ConfigureAwait(false);
            await AtomicWriteAsync(Path.Combine(Folder, BackupFileName(_clock())), json, ct).ConfigureAwait(false);
            Rotate();
            return BackupWriteResult.Ok(SyncPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return BackupWriteResult.Fail(exception.GetType().Name);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Reads the newest valid snapshot, falling back through previous and daily copies.</summary>
    public async Task<BackupDocument?> ReadLatestAsync(CancellationToken ct = default)
    {
        if (await TryReadDocumentAsync(SyncPath, ct).ConfigureAwait(false) is { } current)
        {
            return current;
        }

        if (await TryReadDocumentAsync(PreviousPath, ct).ConfigureAwait(false) is { } previous)
        {
            return previous;
        }

        foreach (var backup in (await ListBackupsAsync(ct).ConfigureAwait(false)).OrderByDescending(b => b.Date))
        {
            if (await TryReadDocumentAsync(backup.Path, ct).ConfigureAwait(false) is { } recovered)
            {
                return recovered;
            }
        }

        return null;
    }

    public async Task<bool> HasUsableBackupAsync(CancellationToken ct = default) =>
        await ReadLatestAsync(ct).ConfigureAwait(false) is { } document && IsMeaningful(document);

    public async Task<IReadOnlyList<BackupFileInfo>> ListBackupsAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        if (!Directory.Exists(Folder))
        {
            return Array.Empty<BackupFileInfo>();
        }

        var files = new List<BackupFileInfo>();
        foreach (var path in Directory.EnumerateFiles(Folder, BackupPrefix + "*.json"))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(path);
            var stamp = name[BackupPrefix.Length..];
            if (DateTime.TryParseExact(stamp, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                var info = new FileInfo(path);
                files.Add(new BackupFileInfo(path, new DateTimeOffset(date), info.Length));
            }
        }

        return files.OrderByDescending(f => f.Date).ToArray();
    }

    /// <summary>Removes daily backups older than the retention window.</summary>
    public void Rotate()
    {
        if (!Directory.Exists(Folder))
        {
            return;
        }

        var cutoff = _clock().Date.AddDays(-RetentionDays);
        foreach (var path in Directory.EnumerateFiles(Folder, BackupPrefix + "*.json"))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            var stamp = name[BackupPrefix.Length..];
            if (DateTime.TryParseExact(stamp, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var date)
                && date < cutoff)
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                    // A file held by the sync client is removed on the next pass.
                }
                catch (UnauthorizedAccessException)
                {
                    // Same as above; rotation never blocks the caller.
                }
            }
        }
    }

    public static bool IsMeaningful(BackupDocument? document) =>
        document is { App: "Hora", Data: not null }
        && (document.Data.Usage?.Count > 0 || document.Data.Settings?.CompletedSetup == true);

    /// <summary>True when the document carries at least one usage row.</summary>
    public static bool HasUsage(BackupDocument? document) =>
        document is { App: "Hora", Data.Usage: { Count: > 0 } };

    private static async Task<BackupDocument?> TryReadDocumentAsync(string path, CancellationToken ct)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            var document = JsonSerializer.Deserialize<BackupDocument>(json, JsonOptions);
            if (document is not { App: "Hora", Data: not null })
            {
                return null;
            }

            // A structurally incomplete payload must be rejected here, before any
            // caller dereferences it.
            if (document.Data.Settings is null || document.Data.Usage is null)
            {
                return null;
            }

            return document;
        }
        catch (Exception exception) when (exception is IOException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    private static async Task AtomicWriteAsync(string target, string content, CancellationToken ct)
    {
        var temporary = $"{target}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, content, ct).ConfigureAwait(false);
            try
            {
                File.Move(temporary, target, overwrite: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The sync client may hold the target open. Keep the existing file
                // until the new content is completely in place.
                File.Copy(temporary, target, overwrite: true);
            }
        }
        finally
        {
            TryDelete(temporary);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A held temp file is removed on the next sweep.
        }
    }

    /// <summary>Removes temp files left behind by an interrupted or crashed write.</summary>
    private void SweepTemporaryFiles()
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(Folder, "hora-*.tmp"))
            {
                if (File.GetLastWriteTimeUtc(path) < DateTime.UtcNow.AddMinutes(-10))
                {
                    TryDelete(path);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Sweeping is best effort.
        }
    }
}
