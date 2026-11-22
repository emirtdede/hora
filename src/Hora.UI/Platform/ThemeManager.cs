using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Hora.Core.Theming;

namespace Hora.UI.Platform;

/// <summary>
/// Applies theme mode plus accent palette to the application resources.
/// Every brush used by the views is defined here, so a theme switch is instant.
/// </summary>
public sealed class ThemeManager : IDisposable
{
    private readonly Application _application;
    private readonly SystemThemeWatcher _watcher;
    private ThemeMode _mode = ThemeMode.Dark;
    private string _accent = "#7C8CF8";
    private bool _reducedMotion;
    private bool _highContrast;

    public ThemeManager(Application application)
    {
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _watcher = new SystemThemeWatcher(_ => Apply(_mode, _accent, _reducedMotion, _highContrast));
    }

    public event EventHandler? ThemeApplied;

    public bool IsDark { get; private set; } = true;

    public string AccentHex => _accent;

    public void Apply(ThemeMode mode, string accentHex, bool reducedMotion, bool highContrast)
    {
        _mode = mode;
        _accent = ThemeCatalog.NormalizeHex(accentHex);
        _reducedMotion = reducedMotion;
        _highContrast = highContrast;

        var dark = ThemeCatalog.IsDark(mode);
        IsDark = dark;

        var background = dark ? "#0D1017" : "#F4F6FB";
        var surface = dark ? "#141924" : "#FFFFFF";
        var surfaceAlt = dark ? "#1A202D" : "#EDF1F9";
        var border = highContrast
            ? (dark ? "#5C6B85" : "#8A94A8")
            : (dark ? "#242B3A" : "#DDE3EF");
        var foreground = dark ? "#EEF1F8" : "#151922";
        var muted = dark ? "#98A2B8" : "#5D6779";

        Set("BrushBackground", background);
        Set("BrushSurface", surface);
        Set("BrushSurfaceAlt", surfaceAlt);
        Set("BrushBorder", border);
        Set("BrushForeground", foreground);
        Set("BrushMuted", muted);
        Set("BrushAccent", _accent);
        Set("BrushAccentSoft", ThemeCatalog.Blend(_accent, background, dark ? 0.72 : 0.82));
        Set("BrushAccentFaint", ThemeCatalog.Blend(_accent, background, dark ? 0.86 : 0.92));
        Set("BrushOnAccent", ThemeCatalog.ContrastForeground(_accent));
        Set("BrushDanger", dark ? "#F87171" : "#C0392B");
        Set("BrushSuccess", dark ? "#34D399" : "#12805C");
        Set("BrushChart1", _accent);
        Set("BrushChart2", ThemeCatalog.Blend(_accent, "#38BDF8", 0.45));
        Set("BrushChart3", ThemeCatalog.Blend(_accent, "#34D399", 0.55));
        Set("BrushChart4", ThemeCatalog.Blend(_accent, "#FBBF24", 0.6));
        Set("BrushChart5", ThemeCatalog.Blend(_accent, "#F472B6", 0.55));
        Set("BrushChart6", ThemeCatalog.Blend(_accent, "#A78BFA", 0.7));
        Set("BrushChart7", ThemeCatalog.Blend(_accent, "#94A3B8", 0.55));
        Set("BrushChart8", ThemeCatalog.Blend(_accent, "#FB923C", 0.8));

        Set("ColorAccent", _accent);
        _application.Resources["ReducedMotion"] = reducedMotion;

        ThemeApplied?.Invoke(this, EventArgs.Empty);
    }

    public static Brush Brush(string key) =>
        Application.Current.TryFindResource(key) as Brush ?? Brushes.Transparent;

    private void Set(string key, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _application.Resources[key] = brush;
        _application.Resources[key + "Color"] = color;
    }

    public static string ToHex(Color color) => string.Create(CultureInfo.InvariantCulture,
        $"#{color.R:X2}{color.G:X2}{color.B:X2}");

    public void Dispose() => _watcher.Dispose();
}
