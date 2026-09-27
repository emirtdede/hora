namespace Hora.Storage.Paths;

public static class HoraPaths
{
    public static string AppDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hora");

    public static string DbPath => Path.Combine(AppDir, "usage.db");

    public static string SettingsPath => Path.Combine(AppDir, "settings.json");

    public static string LogPath => Path.Combine(AppDir, "logs", $"hora-{DateTime.Now:yyyyMMdd}.log");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(AppDir);
        Directory.CreateDirectory(Path.Combine(AppDir, "logs"));
    }
}
