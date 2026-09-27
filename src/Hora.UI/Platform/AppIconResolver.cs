using System.Collections.Concurrent;
using System.Windows.Media;
using Hora.Core.Logging;

namespace Hora.UI.Platform;

public sealed class AppIconResolver
{
    private readonly ConcurrentDictionary<string, ImageSource?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly IHoraLogger _log;

    public AppIconResolver(IHoraLogger log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public ImageSource? Resolve(string exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName) || exeName is "idle" or "locked")
        {
            return null;
        }

        return _cache.GetOrAdd(exeName, Find);
    }

    private ImageSource? Find(string exeName)
    {
        try
        {
            var proc = System.Diagnostics.Process.GetProcessesByName(exeName).FirstOrDefault();
            var path = proc?.MainModule?.FileName;
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, System.Windows.Int32Rect.Empty,
                System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception ex)
        {
            _log.Warn($"Ikon cozule medi ({exeName}): {ex.Message}");
            return null;
        }
    }
}
