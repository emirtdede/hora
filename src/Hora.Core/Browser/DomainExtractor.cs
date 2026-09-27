using System.Windows.Automation;
using Hora.Core.Logging;

namespace Hora.Core.Browser;

public interface IDomainExtractor
{
    string? TryGetDomain(IntPtr hwnd, string exeName);
    string ExtractDomain(string? urlOrText);
}

public sealed class DomainExtractor : IDomainExtractor
{
    private static readonly HashSet<string> BrowserExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "edge", "firefox", "brave", "opera", "vivaldi"
    };

    private readonly IHoraLogger _log;

    public DomainExtractor(IHoraLogger log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public string? TryGetDomain(IntPtr hwnd, string exeName)
    {
        if (hwnd == IntPtr.Zero || !BrowserExe.Contains(exeName))
        {
            return null;
        }

        try
        {
            var task = Task.Run(() => ReadAddressBar(hwnd));
            if (!task.Wait(TimeSpan.FromMilliseconds(300)))
            {
                return null;
            }

            var url = task.Result;
            if (string.IsNullOrWhiteSpace(url))
            {
                return null;
            }

            var domain = ExtractDomain(url);
            return string.IsNullOrEmpty(domain) ? null : domain;
        }
        catch (Exception ex)
        {
            _log.Warn($"Adres çubuğu okunamadı ({exeName}): {ex.Message}");
            return null;
        }
    }

    public string ExtractDomain(string? urlOrText)
    {
        if (string.IsNullOrWhiteSpace(urlOrText))
        {
            return string.Empty;
        }

        var text = urlOrText.Trim();
        if (!text.Contains("://"))
        {
            text = "https://" + text;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            var host = uri.Host.ToLowerInvariant();
            return host.StartsWith("www.", StringComparison.Ordinal) ? host[4..] : host;
        }

        return string.Empty;
    }

    private static string? ReadAddressBar(IntPtr hwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            if (root is null)
            {
                return null;
            }

            var edit = root.FindFirst(TreeScope.Descendants, new PropertyCondition(
                AutomationElement.ControlTypeProperty, ControlType.Edit));
            if (edit is null)
            {
                return null;
            }

            if (edit.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern) && pattern is ValuePattern value)
            {
                return value.Current.Value;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
