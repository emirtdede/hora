using System.IO;
using System.Text.Json;
using Hora.Core.Logging;
using Hora.Storage.Backup;
using Hora.Storage.Db;
using Hora.Storage.Paths;
using Hora.Storage.Settings;

namespace Hora.UI.Services;

/// <summary>Public state of the backup card in Settings.</summary>
public sealed record BackupStatus(
    bool Connected,
    string Provider,
    string Folder,
    bool IsCloud,
    DateTimeOffset? LastSync,
    int RecordCount,
    int LocalRecordCount,
    int BackupCount,
    bool AutoSync,
    bool UsingFallback);

/// <summary>
/// Coordinates the backup folder, snapshot creation, restore, and the debounced
/// automatic sync. All errors are contained; the UI only sees localized results.
/// </summary>
public sealed class BackupService : IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly AppSettings _settings;
    private readonly UsageRepository _repository;
    private readonly IHoraLogger _log;
    private readonly System.Threading.Timer _debounce;
    private readonly object _sync = new();
    private BackupEngine? _engine;
    private bool _usingFallback;
    private bool _dirty;
    private bool _disposed;

    public BackupService(SettingsStore settingsStore, AppSettings settings, UsageRepository repository, IHoraLogger log)
    {
        _settingsStore = settingsStore;
        _settings = settings;
        _repository = repository;
        _log = log;
        _debounce = new System.Threading.Timer(_ => _ = SyncPendingAsync(), null, Timeout.Infinite, Timeout.Infinite);
        ResolveEngine();
    }

    public event EventHandler<BackupStatus>? StatusChanged;

    public BackupEngine? Engine
    {
        get
        {
            lock (_sync)
            {
                return _engine;
            }
        }
    }

    public string CurrentFolder => Engine?.Folder ?? HoraPaths.LocalBackupDir;

    public bool IsConnected => _settings.BackupConnected;

    /// <summary>Recomputes the folder for the configured provider without touching data.</summary>
    public void ResolveEngine()
    {
        var kind = ParseKind(_settings.BackupProvider);
        var root = kind switch
        {
            BackupProviderKind.Custom when !string.IsNullOrWhiteSpace(_settings.BackupCustomRoot) =>
                _settings.BackupCustomRoot,
            BackupProviderKind.Local => HoraPaths.LocalBackupDir,
            _ => BackupLocationDetector.DetectRoot(kind),
        };
        var usingFallback = root is null;
        var folder = root is null ? Path.Combine(HoraPaths.LocalBackupDir, kind.ToString()) : Path.Combine(root, "Hora");
        if (BackupLocationDetector.IsUnsafePath(folder))
        {
            // A rejected custom root must be reported, not hidden behind a local
            // folder that looks connected.
            usingFallback = true;
            folder = HoraPaths.LocalBackupDir;
        }

        lock (_sync)
        {
            _usingFallback = usingFallback;
            _engine = new BackupEngine(folder);
        }
    }

    public static BackupProviderKind ParseKind(string? provider) => provider switch
    {
        "GoogleDrive" => BackupProviderKind.GoogleDrive,
        "Dropbox" => BackupProviderKind.Dropbox,
        "ICloud" => BackupProviderKind.ICloud,
        "Custom" => BackupProviderKind.Custom,
        "Local" => BackupProviderKind.Local,
        _ => BackupProviderKind.OneDrive,
    };

    public async Task<BackupStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var engine = Engine;
        var backups = engine is null ? Array.Empty<BackupFileInfo>() : await engine.ListBackupsAsync(ct).ConfigureAwait(false);
        var document = engine is null ? null : await engine.ReadLatestAsync(ct).ConfigureAwait(false);
        var kind = ParseKind(_settings.BackupProvider);
        var usingFallback = _usingFallback;
        // The summary must describe this device, not only the last backup file,
        // otherwise it reads "0 records" next to a populated dashboard.
        var localRows = await _repository.CountRowsAsync(ct).ConfigureAwait(false);
        return new BackupStatus(
            _settings.BackupConnected,
            kind.ToString(),
            engine?.Folder ?? string.Empty,
            !usingFallback && kind != BackupProviderKind.Local && kind != BackupProviderKind.Custom,
            _settings.LastSyncUtc,
            document?.RecordCount ?? 0,
            (int)Math.Min(int.MaxValue, localRows),
            backups.Count,
            _settings.AutoSync,
            usingFallback);
    }

    /// <summary>Builds the snapshot payload from live state.</summary>
    public async Task<BackupDocument> BuildDocumentAsync(CancellationToken ct = default)
    {
        var rows = await _repository.GetRangeAsync("0000-01-01", "9999-12-31", ct).ConfigureAwait(false);
        var document = new BackupDocument
        {
            Version = "1.0.0",
            Provider = ParseKind(_settings.BackupProvider).ToString(),
            ExportedAt = DateTimeOffset.Now,
            Data = new BackupPayload
            {
                Settings = SettingsSnapshot.From(_settings),
                KnownApplications = (await _repository.GetKnownExecutablesAsync(200, ct).ConfigureAwait(false)).ToList(),
            },
        };
        document.Data.Usage.AddRange(rows.Select(r => new UsageSnapshotRow(r.Date, r.Exe, r.Domain, r.Seconds)));
        return document;
    }

    /// <summary>Writes a snapshot now. Returns the raw status so the caller can localize.</summary>
    public async Task<BackupWriteResult> SyncNowAsync(CancellationToken ct = default)
    {
        var engine = Engine;
        if (engine is null)
        {
            return BackupWriteResult.Fail("NoEngine");
        }

        try
        {
            var document = await BuildDocumentAsync(ct).ConfigureAwait(false);
            var result = await engine.WriteAsync(document, ct).ConfigureAwait(false);
            if (result.Status == BackupWriteStatus.Written)
            {
                _settings.LastSyncUtc = DateTimeOffset.Now;
                _settingsStore.Save(_settings);
            }

            await RaiseStatusAsync().ConfigureAwait(false);
            return result;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            _log.Warn($"Backup write failed: {exception.GetType().Name}");
            return BackupWriteResult.Fail(exception.GetType().Name);
        }
    }

    public async Task<bool> HasRestorableBackupAsync(CancellationToken ct = default)
    {
        var engine = Engine;
        return engine is not null && await engine.HasUsableBackupAsync(ct).ConfigureAwait(false);
    }

    /// <summary>Replaces local data with the newest valid snapshot.</summary>
    public async Task<bool> RestoreAsync(CancellationToken ct = default)
    {
        var engine = Engine;
        if (engine is null)
        {
            return false;
        }

        try
        {
            var document = await engine.ReadLatestAsync(ct).ConfigureAwait(false);
            if (document is null || document.Data.Settings is null)
            {
                return false;
            }

            // Refuse to erase local history with a backup that has no usage rows.
            if (document.Data.Usage.Count == 0 && await _repository.CountRowsAsync(ct).ConfigureAwait(false) > 0)
            {
                _log.Warn("Restore skipped: the backup has no usage rows.");
                return false;
            }

            var configured = await BuildDocumentAsync(ct).ConfigureAwait(false);
            if (configured.Data.Usage.Count > 0)
            {
                // Keep the current local state as the previous snapshot before replacing it.
                await engine.WriteAsync(configured, ct).ConfigureAwait(false);
            }

            await _repository.ReplaceAllAsync(
                document.Data.Usage.Select(r => new Storage.Models.UsageRow(r.Date, r.Exe, r.Domain, r.Seconds)),
                ct).ConfigureAwait(false);

            var restored = document.Data.Settings;
            _settings.Language = Core.Localization.LanguageCatalog.IsSupported(restored.Language)
                ? restored.Language
                : _settings.Language;
            _settings.Theme = Core.Theming.ThemeCatalog.IsKnown(restored.Theme) ? restored.Theme : _settings.Theme;
            _settings.ThemeMode = restored.ThemeMode is "System" or "Light" or "Dark" ? restored.ThemeMode : _settings.ThemeMode;
            _settings.CustomAccent = Core.Theming.ThemeCatalog.NormalizeHex(restored.CustomAccent);
            _settings.IconId = string.IsNullOrWhiteSpace(restored.IconId) ? _settings.IconId : restored.IconId;
            _settings.SavedThemes = (restored.SavedThemes ?? new()).ToList();
            _settings.CompletedSetup = true;
            _settings.BackupConnected = true;
            _settings.LastSyncUtc = DateTimeOffset.Now;
            _settingsStore.Save(_settings);
            await RaiseStatusAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or InvalidOperationException
                                              or Microsoft.Data.Sqlite.SqliteException)
        {
            _log.Warn($"Restore failed: {exception.GetType().Name}");
            return false;
        }
    }

    public async Task<bool> ConnectAsync(CancellationToken ct = default)
    {
        ResolveEngine();

        // Restore instead of overwriting a good backup with an empty local state.
        if (await HasRestorableBackupAsync(ct).ConfigureAwait(false))
        {
            if (await _repository.CountRowsAsync(ct).ConfigureAwait(false) == 0)
            {
                await RestoreAsync(ct).ConfigureAwait(false);
            }
        }

        _settings.BackupConnected = true;
        _settingsStore.Save(_settings);
        await SyncNowAsync(ct).ConfigureAwait(false);
        return true;
    }

    public async Task DisconnectAsync()
    {
        _settings.BackupConnected = false;
        _settingsStore.Save(_settings);
        await RaiseStatusAsync().ConfigureAwait(false);
    }

    /// <summary>Schedules a debounced write, ignoring duplicate triggers within the window.</summary>
    public void MarkDirty()
    {
        if (_disposed || !_settings.BackupConnected || !_settings.AutoSync)
        {
            return;
        }

        lock (_sync)
        {
            if (_dirty)
            {
                return;
            }

            _dirty = true;
        }

        _debounce.Change(350, Timeout.Infinite);
    }

    private async Task SyncPendingAsync()
    {
        lock (_sync)
        {
            _dirty = false;
        }

        if (_disposed || !_settings.BackupConnected || !_settings.AutoSync)
        {
            return;
        }

        await SyncNowAsync().ConfigureAwait(false);
    }

    /// <summary>Flushes a pending debounce synchronously enough to be safe during shutdown.</summary>
    public Task FlushAsync(CancellationToken ct = default) => SyncNowAsync(ct);

    private async Task RaiseStatusAsync()
    {
        var status = await GetStatusAsync().ConfigureAwait(false);
        StatusChanged?.Invoke(this, status);
    }

    public void Dispose()
    {
        _disposed = true;
        _debounce.Dispose();
    }
}
