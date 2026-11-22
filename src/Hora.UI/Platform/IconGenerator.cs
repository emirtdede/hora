using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Hora.Core.Theming;
using Hora.Storage.Paths;

namespace Hora.UI.Platform;

/// <summary>Identifiers for the selectable application icons.</summary>
public sealed record IconOption(string Id, string Accent, string Glyph);

/// <summary>
/// Renders the Hora mark in the selected accent color and produces the ICO, PNG,
/// and WPF image sources used by the window, taskbar, tray, and shortcuts.
/// No external image assets are required at runtime.
/// </summary>
public static class IconGenerator
{
    public static IReadOnlyList<IconOption> Options { get; } = new IconOption[]
    {
        new("aurora", "#7C8CF8", "clock"),
        new("midnight", "#3B4A6B", "clock"),
        new("sunset", "#FB923C", "clock"),
        new("forest", "#34D399", "clock"),
        new("ocean", "#38BDF8", "clock"),
        new("mono", "#F2F4F8", "ring"),
    };

    private static readonly int[] Sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };

    public static IconOption Get(string? id) =>
        Options.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Options[0];

    /// <summary>Writes a multi-resolution ICO and returns its path.</summary>
    public static string EnsureIco(string? iconId)
    {
        var option = Get(iconId);
        Directory.CreateDirectory(HoraPaths.IconDir);
        var path = Path.Combine(HoraPaths.IconDir, $"hora-{option.Id}.ico");
        if (File.Exists(path))
        {
            return path;
        }

        var pngs = Sizes.Select(size => RenderPng(option, size)).ToArray();
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)Sizes.Length);
        var offset = 6 + (16 * Sizes.Length);
        for (var index = 0; index < Sizes.Length; index++)
        {
            var size = Sizes[index];
            var data = pngs[index];
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)(size >= 256 ? 0 : size));
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((ushort)1);
            writer.Write((ushort)32);
            writer.Write(data.Length);
            writer.Write(offset);
            offset += data.Length;
        }

        foreach (var data in pngs)
        {
            writer.Write(data);
        }

        return path;
    }

    public static string EnsurePng(string? iconId, int size = 256)
    {
        var option = Get(iconId);
        Directory.CreateDirectory(HoraPaths.IconDir);
        var path = Path.Combine(HoraPaths.IconDir, $"hora-{option.Id}-{size}.png");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, RenderPng(option, size));
        }

        return path;
    }

    public static byte[] RenderPng(IconOption option, int size)
    {
        using var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.Clear(Color.Transparent);

            var accent = ColorTranslator.FromHtml(option.Accent);
            var scale = size / 256f;
            var inset = 8f * scale;
            var rect = new RectangleF(inset, inset, size - (inset * 2), size - (inset * 2));
            var radius = 56f * scale;
            using (var background = RoundedRect(rect, radius))
            using (var brush = new LinearGradientBrush(
                       new PointF(rect.Left, rect.Top),
                       new PointF(rect.Right, rect.Bottom),
                       Lighten(accent, 0.16f),
                       Darken(accent, 0.18f)))
            {
                graphics.FillPath(brush, background);
            }

            var glyph = option.Glyph == "ring" ? ColorTranslator.FromHtml("#12141C") : ContrastColor(accent);
            var center = new PointF(size / 2f, size / 2f);
            var ringSize = 118f * scale;
            var ringRect = new RectangleF(
                center.X - (ringSize / 2f), center.Y - (ringSize / 2f), ringSize, ringSize);
            using (var pen = new Pen(glyph, 18f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                graphics.DrawEllipse(pen, ringRect);
                var hand = 46f * scale;
                graphics.DrawLine(pen, center, new PointF(center.X, center.Y - hand));
                graphics.DrawLine(pen, center, new PointF(center.X + (hand * 0.72f), center.Y + (hand * 0.42f)));
                graphics.FillEllipse(new SolidBrush(glyph), center.X - (9f * scale), center.Y - (9f * scale),
                    18f * scale, 18f * scale);
            }
        }

        using var output = new MemoryStream();
        bitmap.Save(output, ImageFormat.Png);
        return output.ToArray();
    }

    public static BitmapSource ToBitmapSource(string pngPath)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
        image.UriSource = new Uri(pngPath, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static System.Drawing.Icon LoadIcon(string? iconId)
    {
        var path = EnsureIco(iconId);
        return new System.Drawing.Icon(path);
    }

    private static Color ContrastColor(Color accent)
    {
        var luminance = (0.299 * accent.R) + (0.587 * accent.G) + (0.114 * accent.B);
        return luminance > 170 ? ColorTranslator.FromHtml("#12141C") : Color.White;
    }

    private static Color Lighten(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)Math.Clamp(color.R + ((255 - color.R) * amount), 0, 255),
        (int)Math.Clamp(color.G + ((255 - color.G) * amount), 0, 255),
        (int)Math.Clamp(color.B + ((255 - color.B) * amount), 0, 255));

    private static Color Darken(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)Math.Clamp(color.R * (1 - amount), 0, 255),
        (int)Math.Clamp(color.G * (1 - amount), 0, 255),
        (int)Math.Clamp(color.B * (1 - amount), 0, 255));

    private static GraphicsPath RoundedRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

/// <summary>Points the desktop shortcut at the selected icon, when one exists.</summary>
public static class ShortcutHelper
{
    public static bool TryUpdateIcon(string? iconId, out string error)
    {
        error = string.Empty;
        try
        {
            var ico = IconGenerator.EnsureIco(iconId);
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcut = Path.Combine(desktop, "Hora.lnk");
            if (!File.Exists(shortcut))
            {
                return false;
            }

            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null)
            {
                return false;
            }

            dynamic shell = Activator.CreateInstance(type)!;
            dynamic link = shell.CreateShortcut(shortcut);
            link.IconLocation = ico + ",0";
            link.Save();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or System.Runtime.InteropServices.COMException
                                              or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            error = exception.GetType().Name;
            return false;
        }
    }
}
