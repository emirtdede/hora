using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Hora.Core.Logging;

namespace Hora.UI.Services;

public enum UpdateCheckResult
{
    Latest = 0,
    UpdateAvailable = 1,
    Failed = 2,
}

/// <summary>
/// Lightweight update check against the public GitHub releases feed.
/// Installed builds update through the packaged installer; Store builds opt out
/// because the Store owns delivery.
/// </summary>
public static class UpdateService
{
    private const string FeedUrl = "https://api.github.com/repos/emirtdede/hora/releases/latest";
    private static readonly HttpClient Client = new()
    {
        Timeout = TimeSpan.FromSeconds(12),
    };

    public static bool SelfUpdateSupported => !IsStoreBuild();

    public static async Task<UpdateCheckResult> CheckAsync(IHoraLogger logger, CancellationToken ct = default)
    {
        if (!SelfUpdateSupported)
        {
            return UpdateCheckResult.Failed;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, FeedUrl);
            request.Headers.UserAgent.ParseAdd("Hora-Updater/1.0");
            using var response = await Client.SendAsync(request, ct).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Failed;
            }

            using var document = await response.Content.ReadFromJsonAsync<JsonDocument>(ct).ConfigureAwait(false);
            if (document is null
                || !document.RootElement.TryGetProperty("tag_name", out var tag)
                || tag.GetString() is not { } raw)
            {
                return UpdateCheckResult.Failed;
            }

            var remote = ParseVersion(raw);
            var local = typeof(UpdateService).Assembly.GetName().Version ?? new Version(1, 0, 0);
            return remote > local ? UpdateCheckResult.UpdateAvailable : UpdateCheckResult.Latest;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              or JsonException or NotSupportedException)
        {
            logger.Warn($"Update check failed: {exception.GetType().Name}");
            return UpdateCheckResult.Failed;
        }
    }

    public static Version ParseVersion(string tag)
    {
        var value = (tag ?? string.Empty).Trim().TrimStart('v', 'V');
        return Version.TryParse(value, out var version) ? version : new Version(0, 0, 0);
    }

    private static bool IsStoreBuild()
    {
        try
        {
            var process = Environment.ProcessPath ?? string.Empty;
            var windowsApps = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps");
            return process.StartsWith(windowsApps, StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
