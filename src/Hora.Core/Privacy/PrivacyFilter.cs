namespace Hora.Core.Privacy;

public interface IPrivacyFilter
{
    bool IsSensitive(string exeName, string domain);
    string SanitizeTitle(string exeName, string domain, string title);
}

public sealed class PrivacyFilter : IPrivacyFilter
{
    private static readonly HashSet<string> SensitiveExe = new(StringComparer.OrdinalIgnoreCase)
    {
        "keepass", "keepassxc", "1password", "bitwarden", "lastpass", "dashlane"
    };

    private static readonly string[] SensitiveDomainParts =
    {
        "bank", "ziraat", "garanti", "akbank", "yapikredi", "isbank", "halkbank", "vakifbank",
        "paypal", "ebanking", "sifre", "password", "cuzdan", "wallet"
    };

    public const string Masked = "[gizli]";

    public bool IsSensitive(string exeName, string domain)
    {
        if (SensitiveExe.Contains(exeName))
        {
            return true;
        }

        var d = domain.ToLowerInvariant();
        return SensitiveDomainParts.Any(p => d.Contains(p, StringComparison.Ordinal));
    }

    public string SanitizeTitle(string exeName, string domain, string title)
    {
        if (IsSensitive(exeName, domain))
        {
            return Masked;
        }

        if (title.Length > 200)
        {
            return title[..200];
        }

        return title;
    }
}
