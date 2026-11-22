using System.IO;
using Hora.Core.Localization;
using Hora.Core.Logging;
using Hora.Core.Theming;
using Hora.Storage.Backup;
using Hora.Storage.Db;
using Hora.Storage.Paths;
using Hora.Storage.Settings;
using Hora.UI.Platform;
using Hora.UI.Services;
using Hora.UI.Tracking;

namespace Hora.UI;

/// <summary>
/// Single composition root for the application. Views receive this instance and
/// never construct services themselves.
/// </summary>
public sealed class AppContext : IDisposable
{
    private readonly System.Windows.Threading.DispatcherTimer _deferredSave = new()
    {
        Interval = TimeSpan.FromMilliseconds(350),
    };

    public AppContext(
        AppSettings settings,
        SettingsStore settingsStore,
        UsageRepository repository,
        TrackingEngine engine,
        IHoraLogger logger)
    {
        Settings = settings;
        SettingsStore = settingsStore;
        Repository = repository;
        Engine = engine;
        Logger = logger;
        Backup = new BackupService(settingsStore, settings, repository, logger);
        Translator.Instance.SetLanguage(settings.Language);
        _deferredSave.Tick += OnDeferredSave;
    }

    public AppSettings Settings { get; }

    public SettingsStore SettingsStore { get; }

    public UsageRepository Repository { get; }

    public TrackingEngine Engine { get; }

    public IHoraLogger Logger { get; }

    public BackupService Backup { get; }

    public ThemeManager? ThemeManager { get; set; }

    /// <summary>
    /// Result of the last global shortcut registration attempt. The settings page
    /// reads it so it can report an unusable combination instead of claiming success.
    /// </summary>
    public bool HotkeyAvailable { get; set; } = true;

    public event EventHandler? SettingsChanged;

    public event EventHandler? DataChanged;

    public event EventHandler? LanguageChanged;

    public string T(string key) => Translator.Instance.Get(key);

    public string T(string key, params object?[] args) => Translator.Instance.Format(key, args);

    public void SaveSettings(bool notify = true)
    {
        _deferredSave.Stop();
        SettingsStore.Save(Settings);
        Backup.MarkDirty();
        if (notify)
        {
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Applies a value that changes continuously (zoom, text scale) without one
    /// disk write and one shell refresh per slider tick.
    /// </summary>
    public void SaveSettingsDeferred()
    {
        _deferredSave.Stop();
        _deferredSave.Start();
    }

    private void OnDeferredSave(object? sender, EventArgs e)
    {
        _deferredSave.Stop();
        SaveSettings();
    }

    public void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Catalog key for a backup provider. The rail footer and the Backup page must
    /// never disagree, so both call this one mapping.
    /// </summary>
    public static string ProviderLabelKey(string? provider) => provider switch
    {
        "GoogleDrive" => "backupGoogleDrive",
        "Dropbox" => "backupDropbox",
        "ICloud" => "backupICloud",
        "Custom" => "backupCustom",
        "Local" => "backupLocal",
        _ => "backupOneDrive",
    };

    public void ApplyTheme()
    {
        var mode = Settings.ThemeMode switch
        {
            "Light" => ThemeMode.Light,
            "Dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };

        var accent = Settings.Theme == ThemeCatalog.CustomId
            ? Settings.CustomAccent
            : ThemeCatalog.Get(Settings.Theme).Accent;

        ThemeManager?.Apply(mode, accent, Settings.ReducedMotion, Settings.HighContrast);
    }

    public void ApplyLanguage(string languageId)
    {
        if (!LanguageCatalog.IsSupported(languageId))
        {
            return;
        }

        Settings.Language = LanguageCatalog.Get(languageId).Id;
        Translator.Instance.SetLanguage(Settings.Language);
        SaveSettings(notify: false);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
        LocalizationProxy.Instance.RaiseAll();
    }

    public string BackupFolder
    {
        get
        {
            var instance = Backup.Engine;
            return instance?.Folder ?? HoraPaths.LocalBackupDir;
        }
    }

    public void EnsureBackupFolder()
    {
        var folder = BackupFolder;
        if (!BackupLocationDetector.IsUnsafePath(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public void Dispose()
    {
        _deferredSave.Stop();
        Backup.Dispose();
        ThemeManager?.Dispose();
    }
}
