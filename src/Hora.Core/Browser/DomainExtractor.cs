using System.Text.RegularExpressions;

namespace Hora.Core.Browser;

public interface IDomainExtractor
{
    string? Extract(string? windowTitle, string? exe);
}

/// <summary>
/// Extracts a registrable domain from a browser window title.
/// Titles follow the "Page title - Browser" convention on Windows.
/// </summary>
public sealed partial class DomainExtractor : IDomainExtractor
{
    private static readonly HashSet<string> BrowserExecutables = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "arc", "librewolf",
        "waterfox", "chromium", "thorium", "zen", "floorp",
    };

    private static readonly HashSet<string> MultiPartSuffixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "co.uk", "org.uk", "ac.uk", "gov.uk", "com.tr", "net.tr", "org.tr", "gov.tr", "edu.tr",
        "com.au", "net.au", "org.au", "co.jp", "or.jp", "ne.jp", "co.kr", "or.kr", "com.br",
        "com.mx", "com.ar", "co.in", "co.za", "com.cn", "com.tw", "co.nz", "com.sg", "com.ua",
        "co.il", "com.pl", "com.es", "com.pt", "com.gr", "com.ru", "co.id", "com.my", "com.ph",
    };

    public string? Extract(string? windowTitle, string? exe)
    {
        if (string.IsNullOrWhiteSpace(windowTitle) || !IsBrowser(exe))
        {
            return null;
        }

        var match = DomainPattern().Match(windowTitle);
        if (!match.Success)
        {
            return null;
        }

        return Normalize(match.Groups["host"].Value);
    }

    public static bool IsBrowser(string? exe) =>
        !string.IsNullOrWhiteSpace(exe) && BrowserExecutables.Contains(exe.Trim());

    private static string Normalize(string host)
    {
        var value = host.ToLowerInvariant().Trim('.');
        if (value.StartsWith("www.", StringComparison.Ordinal))
        {
            value = value[4..];
        }

        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2)
        {
            return value;
        }

        var lastTwo = string.Join('.', parts[^2..]);
        if (MultiPartSuffixes.Contains(lastTwo) && parts.Length >= 3)
        {
            return string.Join('.', parts[^3..]);
        }

        return lastTwo;
    }

    [GeneratedRegex(@"^(?:.*?\s-\s)?(?<host>(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z]{2,24})(?::\d{2,5})?(?:\s*[-|].*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();
}
