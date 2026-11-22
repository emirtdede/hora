using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hora.Storage.Settings;

/// <summary>All persisted user preferences. Every property has a safe default.</summary>
public sealed class AppSettings
{
    public double Left { get; set; } = 120;
    public double Top { get; set; } = 90;
    public double Width { get; set; } = 1180;
    public double Height { get; set; } = 780;
    public double Opacity { get; set; } = 1.0;
    public string View { get; set; } = "List";

    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "aurora";
    public string ThemeMode { get; set; } = "System";
    public string CustomAccent { get; set; } = "#7C8CF8";
    public List<string> SavedThemes { get; set; } = new();
    public string IconId { get; set; } = "aurora";

    public bool LaunchOnStartup { get; set; } = true;
    public bool SilentStart { get; set; } = true;
    public string CloseBehavior { get; set; } = "ask";
    public bool JumpList { get; set; } = true;
    public bool Notifications { get; set; } = true;
    public bool NotificationSound { get; set; } = true;
    public string Hotkey { get; set; } = "Ctrl+Alt+H";

    public double UiScale { get; set; } = 1.0;
    public double FontScale { get; set; } = 1.0;
    public bool ReducedMotion { get; set; }
    public bool HighContrast { get; set; }

    public string BackupProvider { get; set; } = "OneDrive";
    public string BackupCustomRoot { get; set; } = string.Empty;
    public bool BackupConnected { get; set; }
    public bool AutoSync { get; set; }
    public DateTimeOffset? LastSyncUtc { get; set; }
    public bool RestorePrompted { get; set; }

    public bool CompletedSetup { get; set; }
    public bool RememberCloseChoice { get; set; }
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    [JsonIgnore]
    public bool FirstRun => !CompletedSetup;

    public AppSettings Clone()
    {
        var json = JsonSerializer.Serialize(this);
        return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }
}

/// <summary>Loads and atomically saves settings. Failures never block the application.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // A hand-edited or externally generated file may use PascalCase; it must
        // still load instead of silently falling back to defaults.
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly string _path;
    private readonly object _sync = new();

    public SettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public string Path => _path;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
            return Sanitize(settings);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        if (settings is null)
        {
            return;
        }

        try
        {
            lock (_sync)
            {
                var directory = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var clean = Sanitize(settings.Clone());
                var json = JsonSerializer.Serialize(clean, Options);
                var temporary = _path + $".tmp-{Environment.ProcessId}";
                File.WriteAllText(temporary, json);
                try
                {
                    // File.Replace keeps the previous file as a backup, so a reader
                    // never observes a half-written settings file.
                    if (File.Exists(_path))
                    {
                        File.Replace(temporary, _path, null, ignoreMetadataErrors: true);
                    }
                    else
                    {
                        File.Move(temporary, _path);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    File.Move(temporary, _path, overwrite: true);
                }
                finally
                {
                    TryDelete(temporary);
                }
            }
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException
                                              or JsonException
                                              or ArgumentException)
        {
            // A failed write must never surface to the user or crash the app.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Ignored on purpose.
        }
    }

    public static AppSettings Sanitize(AppSettings? settings)
    {
        settings ??= new AppSettings();
        settings.Width = Clamp(settings.Width, 900, 2400, 1180);
        settings.Height = Clamp(settings.Height, 620, 1600, 780);
        settings.Opacity = Clamp(settings.Opacity, 0.7, 1.0, 1.0);
        settings.Left = Clamp(settings.Left, -2000, 6000, 120);
        settings.Top = Clamp(settings.Top, -2000, 4000, 90);
        settings.UiScale = Clamp(settings.UiScale, 0.8, 1.5, 1.0);
        settings.FontScale = Clamp(settings.FontScale, 0.9, 1.4, 1.0);

        if (!Core.Localization.LanguageCatalog.IsSupported(settings.Language))
        {
            settings.Language = Core.Localization.LanguageCatalog.FallbackId;
        }

        if (!Core.Theming.ThemeCatalog.IsKnown(settings.Theme))
        {
            settings.Theme = Core.Theming.ThemeCatalog.DefaultId;
        }

        settings.Theme = string.Equals(settings.Theme, Core.Theming.ThemeCatalog.CustomId, StringComparison.OrdinalIgnoreCase)
            ? Core.Theming.ThemeCatalog.CustomId
            : settings.Theme.ToLower(CultureInfo.InvariantCulture);

        if (settings.ThemeMode is not ("System" or "Light" or "Dark"))
        {
            settings.ThemeMode = "System";
        }

        settings.CustomAccent = Core.Theming.ThemeCatalog.NormalizeHex(settings.CustomAccent);
        settings.SavedThemes = (settings.SavedThemes ?? new())
            .Where(t => Core.Theming.ThemeCatalog.IsValidHex(t))
            .Select(Core.Theming.ThemeCatalog.NormalizeHex)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(12)
            .ToList();

        if (settings.View is not ("List" or "Chart" or "Mini"))
        {
            settings.View = "List";
        }

        if (settings.CloseBehavior is not ("ask" or "tray" or "exit"))
        {
            settings.CloseBehavior = "ask";
        }

        if (settings.BackupProvider is not ("OneDrive" or "GoogleDrive" or "Dropbox" or "ICloud" or "Custom" or "Local"))
        {
            settings.BackupProvider = "OneDrive";
        }

        if (string.IsNullOrWhiteSpace(settings.IconId))
        {
            settings.IconId = "aurora";
        }

        if (!Core.Input.HotkeyCatalog.IsValid(settings.Hotkey))
        {
            settings.Hotkey = Core.Input.HotkeyCatalog.Default;
        }

        return settings;
    }

    /// <summary>Rejects NaN and infinity before clamping, because Clamp(NaN) returns NaN.</summary>
    private static double Clamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
