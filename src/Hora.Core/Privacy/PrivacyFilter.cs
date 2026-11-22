using System.Text.RegularExpressions;

namespace Hora.Core.Privacy;

public interface IPrivacyFilter
{
    bool IsSensitive(string? title, string? exe);
    string SanitizeTitle(string? title, string? exe);
}

/// <summary>
/// Hides window titles for password managers, banking, and health portals.
/// The measured duration is still recorded; only the title text is dropped.
/// </summary>
public sealed partial class PrivacyFilter : IPrivacyFilter
{
    private static readonly string[] SensitiveExecutables =
    {
        "keepass", "keepassxc", "bitwarden", "1password", "lastpass", "dashlane", "enpass",
        "roboform", "nordpass", "keeper", "buttercup", "password", "protonpass", "safeincloud",
    };

    private static readonly string[] SensitiveKeywords =
    {
        "password", "passwd", "sign in", "signin", "log in", "login", "two-factor", "2fa",
        "bank", "banking", "wallet", "crypto seed", "recovery phrase", "credit card",
        "checkout", "payment", "invoice", "tax", "hastane", "hospital", "clinic", "patient",
        "medical", "saglik", "e-nabiz", "enabiz", "sigorta", "insurance", "bordro", "payroll",
        // Turkish credential and banking screens: "Giris", "Sifre", "Oturum ac", "IBAN".
        "giris", "giriş", "sifre", "şifre", "oturum", "hesap", "iban", "dogrulama", "doğrulama",
        "kredi karti", "kredi kartı", "banka", "odeme", "ödeme",
    };

    public bool IsSensitive(string? title, string? exe)
    {
        var app = (exe ?? string.Empty).ToLowerInvariant();
        foreach (var name in SensitiveExecutables)
        {
            if (app.Contains(name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var lowered = title.ToLowerInvariant();
        foreach (var keyword in SensitiveKeywords)
        {
            if (lowered.Contains(keyword, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return CardNumberPattern().IsMatch(title);
    }

    public string SanitizeTitle(string? title, string? exe)
    {
        if (IsSensitive(title, exe))
        {
            return string.Empty;
        }

        return Compress(title);
    }

    private static string Compress(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var collapsed = WhitespacePattern().Replace(title, " ").Trim();
        return collapsed.Length > 160 ? collapsed[..160].TrimEnd() + "..." : collapsed;
    }

    [GeneratedRegex(@"\b(?:\d[ -]?){13,19}\b")]
    private static partial Regex CardNumberPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
