using System.Globalization;

namespace Hora.Core.Theming;

/// <summary>Named accent palettes available in Settings.</summary>
public sealed record ThemePalette(string Id, string Accent, string AccentSoft);

/// <summary>The visual mode that drives surface colors.</summary>
public enum ThemeMode
{
    System = 0,
    Light = 1,
    Dark = 2,
}

public static class ThemeCatalog
{
    public const string DefaultId = "aurora";
    public const string CustomId = "custom";

    public static IReadOnlyList<ThemePalette> All { get; } = new ThemePalette[]
    {
        new("aurora", "#7C8CF8", "#3B4699"),
        new("midnight", "#5B8DEF", "#2A4585"),
        new("ocean", "#38BDF8", "#1F6F8B"),
        new("forest", "#34D399", "#1B6B52"),
        new("sunset", "#FB923C", "#8A4A1B"),
        new("rose", "#F472B6", "#87315F"),
        new("amber", "#FBBF24", "#7C5A12"),
        new("violet", "#A78BFA", "#5C4894"),
        new("slate", "#94A3B8", "#475569"),
        new("crimson", "#F87171", "#8B3030"),
        new("teal", "#2DD4BF", "#166B62"),
        new("indigo", "#818CF8", "#3F4A9E"),
        new("coral", "#FF8A80", "#8C4038"),
        new("lime", "#A3E635", "#4E7016"),
        new("sky", "#60A5FA", "#2E5D91"),
        new("plum", "#C084FC", "#6B3E8F"),
        new("sand", "#D6C08A", "#77653C"),
        new("mint", "#6EE7B7", "#2F7A5F"),
        new("copper", "#D98C5F", "#7A4B31"),
        new("steel", "#7DD3FC", "#2F6B85"),
        new("cherry", "#FB7185", "#8B3140"),
        new("lagoon", "#22D3EE", "#0E6C7A"),
        new("mustard", "#E5C07B", "#7A6335"),
        new("orchid", "#E879F9", "#84368F"),
        new("graphite", "#CBD5E1", "#4B5563"),
        new("emerald", "#10B981", "#0C6247"),
        new("sapphire", "#4F7CFF", "#274694"),
        new("blush", "#F9A8D4", "#8B466F"),
    };

    public static ThemePalette Get(string? id)
    {
        if (id == CustomId)
        {
            return new ThemePalette(CustomId, "#7C8CF8", "#3B4699");
        }

        return All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? All[0];
    }

    public static bool IsKnown(string? id) =>
        id == CustomId || All.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

    public static (byte R, byte G, byte B) ParseHex(string? hex)
    {
        var value = (hex ?? string.Empty).Trim().TrimStart('#');
        if (value.Length == 3)
        {
            value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
        }

        if (value.Length != 6 || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var packed))
        {
            return (124, 140, 248);
        }

        return ((byte)((packed >> 16) & 0xFF), (byte)((packed >> 8) & 0xFF), (byte)(packed & 0xFF));
    }

    public static bool IsValidHex(string? hex)
    {
        var value = (hex ?? string.Empty).Trim().TrimStart('#');
        return value.Length is 3 or 6 && value.All(Uri.IsHexDigit);
    }

    public static string NormalizeHex(string? hex)
    {
        if (!IsValidHex(hex))
        {
            return "#7C8CF8";
        }

        var value = hex!.Trim().TrimStart('#').ToUpperInvariant();
        if (value.Length == 3)
        {
            value = string.Concat(value[0], value[0], value[1], value[1], value[2], value[2]);
        }

        return "#" + value;
    }

    /// <summary>Picks a readable foreground color for the given accent.</summary>
    public static string ContrastForeground(string? accentHex)
    {
        var (r, g, b) = ParseHex(accentHex);
        var luminance = (0.299 * r) + (0.587 * g) + (0.114 * b);
        return luminance > 160 ? "#101322" : "#FFFFFF";
    }

    public static string Blend(string first, string second, double amount)
    {
        var (r1, g1, b1) = ParseHex(first);
        var (r2, g2, b2) = ParseHex(second);
        var t = Math.Clamp(amount, 0, 1);
        return string.Create(CultureInfo.InvariantCulture,
            $"#{(int)Math.Round(r1 + ((r2 - r1) * t)):X2}{(int)Math.Round(g1 + ((g2 - g1) * t)):X2}{(int)Math.Round(b1 + ((b2 - b1) * t)):X2}");
    }

    public static bool IsDark(ThemeMode mode)
    {
        if (mode != ThemeMode.System)
        {
            return mode == ThemeMode.Dark;
        }

        return SystemIsDark();
    }

    private static bool SystemIsDark()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch
        {
            return true;
        }
    }
}
