using System.Text.Json;

namespace Hora.Storage.Settings;

public sealed class AppSettings
{
    public double Left { get; set; } = 100;
    public double Top { get; set; } = 100;
    public double Width { get; set; } = 340;
    public double Height { get; set; } = 500;
    public double Opacity { get; set; } = 0.96;
    public string Theme { get; set; } = "Black";
    public string View { get; set; } = "Index";
    public bool LaunchOnStartup { get; set; } = true;
    public bool FirstRun { get; set; } = true;
}

public sealed class SettingsStore
{
    private readonly string _path;

    public SettingsStore(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            return Sanitize(settings);
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var clean = Sanitize(settings);
            var json = JsonSerializer.Serialize(clean, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_path, json);
        }
        catch
        {
            // Ayar kaydı asla çökertmez.
        }
    }

    private static AppSettings Sanitize(AppSettings? s)
    {
        s ??= new AppSettings();
        s.Width = Math.Clamp(s.Width, 260, 800);
        s.Height = Math.Clamp(s.Height, 300, 900);
        s.Opacity = Math.Clamp(s.Opacity, 0.4, 1.0);
        s.Left = Math.Clamp(s.Left, -1000, 4000);
        s.Top = Math.Clamp(s.Top, -1000, 4000);
        if (s.Theme == "Dark")
        {
            s.Theme = "Black";
        }
        else if (s.Theme == "Light")
        {
            s.Theme = "White";
        }

        if (s.Theme != "Black" && s.Theme != "White" && s.Theme != "Glass")
        {
            s.Theme = "Black";
        }

        if (s.View != "Mini" && s.View != "Index" && s.View != "Spectrum")
        {
            s.View = "Index";
        }

        return s;
    }
}
