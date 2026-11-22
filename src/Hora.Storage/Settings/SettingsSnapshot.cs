namespace Hora.Storage.Settings;

/// <summary>The portable subset of settings that is written to a backup.</summary>
public sealed class SettingsSnapshot
{
    public string Language { get; set; } = "en";

    public string Theme { get; set; } = "aurora";

    public string ThemeMode { get; set; } = "System";

    public string CustomAccent { get; set; } = "#7C8CF8";

    public string IconId { get; set; } = "aurora";

    public bool CompletedSetup { get; set; }

    public List<string> SavedThemes { get; set; } = new();

    public static SettingsSnapshot From(AppSettings settings) => new()
    {
        Language = settings.Language,
        Theme = settings.Theme,
        ThemeMode = settings.ThemeMode,
        CustomAccent = settings.CustomAccent,
        IconId = settings.IconId,
        CompletedSetup = settings.CompletedSetup,
        SavedThemes = (settings.SavedThemes ?? new()).ToList(),
    };
}
