using Hora.Storage.Settings;
using Xunit;

namespace Hora.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void Sanitize_clamps_window_and_scale_values()
    {
        var settings = SettingsStore.Sanitize(new AppSettings
        {
            Width = 100,
            Height = 5000,
            UiScale = 9,
            FontScale = 0.1,
        });

        Assert.Equal(900, settings.Width);
        Assert.Equal(1600, settings.Height);
        Assert.Equal(1.5, settings.UiScale);
        Assert.Equal(0.9, settings.FontScale);
    }

    [Fact]
    public void Sanitize_rejects_unknown_theme_and_language()
    {
        var settings = SettingsStore.Sanitize(new AppSettings
        {
            Theme = "nope",
            Language = "xx",
            ThemeMode = "Rainbow",
            CloseBehavior = "explode",
            Hotkey = "not a gesture",
        });

        Assert.Equal("aurora", settings.Theme);
        Assert.Equal("en", settings.Language);
        Assert.Equal("System", settings.ThemeMode);
        Assert.Equal("ask", settings.CloseBehavior);
        Assert.Equal("Ctrl+Alt+H", settings.Hotkey);
    }

    [Fact]
    public void Sanitize_keeps_valid_custom_accent()
    {
        var settings = SettingsStore.Sanitize(new AppSettings { CustomAccent = "aabbcc" });
        Assert.Equal("#AABBCC", settings.CustomAccent);
    }

    [Fact]
    public void Sanitize_defaults_completely_missing_values()
    {
        var settings = SettingsStore.Sanitize(null);
        Assert.Equal(1180, settings.Width);
        Assert.False(settings.CompletedSetup);
    }

    [Fact]
    public void Snapshot_round_trips_the_portable_fields()
    {
        var settings = new AppSettings
        {
            Language = "de",
            Theme = "ocean",
            ThemeMode = "Dark",
            CustomAccent = "#123456",
            IconId = "midnight",
            CompletedSetup = true,
            SavedThemes = new List<string> { "#111111" },
        };

        var snapshot = SettingsSnapshot.From(settings);
        Assert.Equal("de", snapshot.Language);
        Assert.Equal("ocean", snapshot.Theme);
        Assert.Equal("Dark", snapshot.ThemeMode);
        Assert.True(snapshot.CompletedSetup);
        Assert.Single(snapshot.SavedThemes);
    }

    [Fact]
    public void Save_and_load_round_trip_preserves_language()
    {
        var root = Path.Combine(Path.GetTempPath(), "hora-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            var store = new SettingsStore(path);
            var settings = store.Load();
            settings.Language = "fr";
            settings.Theme = "midnight";
            store.Save(settings);

            var reloaded = new SettingsStore(path).Load();
            Assert.Equal("fr", reloaded.Language);
            Assert.Equal("midnight", reloaded.Theme);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void PascalCase_settings_file_loads_instead_of_resetting()
    {
        var root = Path.Combine(Path.GetTempPath(), "hora-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, """
                {
                  "Width": 1180,
                  "CompletedSetup": true,
                  "Language": "de",
                  "ThemeMode": "Dark"
                }
                """);
            var settings = new SettingsStore(path).Load();
            Assert.True(settings.CompletedSetup);
            Assert.Equal("de", settings.Language);
            Assert.Equal("Dark", settings.ThemeMode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Corrupt_settings_file_falls_back_to_defaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "hora-settings", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "settings.json");
            File.WriteAllText(path, "{ broken");
            var settings = new SettingsStore(path).Load();
            Assert.Equal("en", settings.Language);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
