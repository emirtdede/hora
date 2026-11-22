using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Hora.Core.Localization;

/// <summary>Describes one supported interface language.</summary>
/// <param name="Id">Stable identifier used in settings and resource names.</param>
/// <param name="EnglishName">English display name.</param>
/// <param name="NativeName">Name rendered in the language itself.</param>
/// <param name="Rtl">True when the language is written right to left.</param>
public sealed record LanguageInfo(string Id, string EnglishName, string NativeName, bool Rtl = false)
{
    public string CultureName => Id switch
    {
        "zh-tw" => "zh-TW",
        "fil" => "fil-PH",
        "no" => "nb-NO",
        _ => Id,
    };
}

/// <summary>The complete catalog of 73 supported languages.</summary>
public static class LanguageCatalog
{
    public const string FallbackId = "en";

    public static IReadOnlyList<LanguageInfo> All { get; } = new LanguageInfo[]
    {
        new("tr", "Turkish", "Turkce"),
        new("en", "English", "English"),
        new("de", "German", "Deutsch"),
        new("es", "Spanish", "Espanol"),
        new("fr", "French", "Francais"),
        new("it", "Italian", "Italiano"),
        new("pt", "Portuguese", "Portugues"),
        new("nl", "Dutch", "Nederlands"),
        new("pl", "Polish", "Polski"),
        new("ru", "Russian", "Russkiy"),
        new("ja", "Japanese", "Nihongo"),
        new("ko", "Korean", "Hangugeo"),
        new("zh", "Chinese (Simplified)", "Jian ti zhong wen"),
        new("zh-tw", "Chinese (Traditional)", "Fan ti zhong wen"),
        new("sv", "Swedish", "Svenska"),
        new("ar", "Arabic", "Al arabiyya", true),
        new("hi", "Hindi", "Hindi"),
        new("id", "Indonesian", "Bahasa Indonesia"),
        new("vi", "Vietnamese", "Tieng Viet"),
        new("uk", "Ukrainian", "Ukrainska"),
        new("el", "Greek", "Ellinika"),
        new("cs", "Czech", "Cestina"),
        new("da", "Danish", "Dansk"),
        new("fi", "Finnish", "Suomi"),
        new("no", "Norwegian", "Norsk"),
        new("hu", "Hungarian", "Magyar"),
        new("ro", "Romanian", "Romana"),
        new("th", "Thai", "Phasa thai"),
        new("ms", "Malay", "Bahasa Melayu"),
        new("fil", "Filipino", "Filipino"),
        new("bn", "Bengali", "Bangla"),
        new("he", "Hebrew", "Ivrit", true),
        new("fa", "Persian", "Farsi", true),
        new("ur", "Urdu", "Urdu", true),
        new("az", "Azerbaijani", "Azerbaycan dili"),
        new("kk", "Kazakh", "Qazaq tili"),
        new("uz", "Uzbek", "Ozbekcha"),
        new("bg", "Bulgarian", "Balgarski"),
        new("sk", "Slovak", "Slovencina"),
        new("hr", "Croatian", "Hrvatski"),
        new("et", "Estonian", "Eesti"),
        new("ta", "Tamil", "Tamil"),
        new("te", "Telugu", "Telugu"),
        new("mr", "Marathi", "Marathi"),
        new("kn", "Kannada", "Kannada"),
        new("gu", "Gujarati", "Gujarati"),
        new("ml", "Malayalam", "Malayalam"),
        new("pa", "Punjabi", "Punjabi"),
        new("lt", "Lithuanian", "Lietuviu"),
        new("lv", "Latvian", "Latviesu"),
        new("sr", "Serbian", "Srpski"),
        new("sl", "Slovenian", "Slovenscina"),
        new("bs", "Bosnian", "Bosanski"),
        new("sq", "Albanian", "Shqip"),
        new("mk", "Macedonian", "Makedonski"),
        new("is", "Icelandic", "Islenska"),
        new("sw", "Swahili", "Kiswahili"),
        new("am", "Amharic", "Amarigna"),
        new("ha", "Hausa", "Hausa"),
        new("yo", "Yoruba", "Yoruba"),
        new("af", "Afrikaans", "Afrikaans"),
        new("ka", "Georgian", "Kartuli"),
        new("hy", "Armenian", "Hayeren"),
        new("ky", "Kyrgyz", "Kyrgyzcha"),
        new("tk", "Turkmen", "Turkmen dili"),
        new("mn", "Mongolian", "Mongol hel"),
        new("ca", "Catalan", "Catala"),
        new("eu", "Basque", "Euskara"),
        new("gl", "Galician", "Galego"),
        new("ga", "Irish", "Gaeilge"),
        new("my", "Burmese", "Myanmar"),
        new("km", "Khmer", "Khmer"),
        new("si", "Sinhala", "Sinhala"),
    };

    public static bool IsSupported(string? id) =>
        !string.IsNullOrWhiteSpace(id) && All.Any(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase));

    public static LanguageInfo Get(string? id) =>
        All.FirstOrDefault(l => string.Equals(l.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? All.First(l => l.Id == FallbackId);

    /// <summary>Resolves a Windows UI culture to a supported language, falling back to English.</summary>
    public static string ResolveFromCulture(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return FallbackId;
        }

        var normalized = name.Replace('_', '-').Trim();
        var exact = All.FirstOrDefault(l => string.Equals(l.CultureName, normalized, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact.Id;
        }

        var two = normalized.Split('-')[0];
        var match = All.FirstOrDefault(l => string.Equals(l.Id, two, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? FallbackId;
    }
}

/// <summary>Loads embedded JSON catalogs and exposes translated strings.</summary>
public sealed class Translator
{
    private static readonly Lazy<IReadOnlyDictionary<string, string>> English = new(() => LoadCatalog(LanguageCatalog.FallbackId));
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public static Translator Instance { get; } = new();

    public string LanguageId { get; private set; } = LanguageCatalog.FallbackId;

    public LanguageInfo Language => LanguageCatalog.Get(LanguageId);

    public event EventHandler? LanguageChanged;

    public void SetLanguage(string? id)
    {
        var resolved = LanguageCatalog.IsSupported(id) ? LanguageCatalog.Get(id).Id : LanguageCatalog.FallbackId;
        if (string.Equals(resolved, LanguageId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LanguageId = resolved;
        CultureInfo.DefaultThreadCurrentUICulture = SafeCulture(resolved);
        CultureInfo.CurrentUICulture = SafeCulture(resolved);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string this[string key] => Get(key);

    public string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        if (Catalog(LanguageId).TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return English.Value.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public string Format(string key, params object?[] args)
    {
        try
        {
            return string.Format(SafeCulture(LanguageId), Get(key), args);
        }
        catch (FormatException)
        {
            return Get(key);
        }
    }

    public IReadOnlyDictionary<string, string> Catalog(string id) =>
        _cache.TryGetValue(id, out var cached) ? cached : LoadCatalog(id);

    public static IReadOnlyCollection<string> Keys(string id) => LoadCatalog(id).Keys.ToArray();

    private IReadOnlyDictionary<string, string> LoadCached(string id)
    {
        if (!_cache.TryGetValue(id, out var cached))
        {
            cached = LoadCatalog(id);
            _cache[id] = cached;
        }

        return cached;
    }

    private static IReadOnlyDictionary<string, string> LoadCatalog(string id)
    {
        var assembly = typeof(Translator).Assembly;
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(name => name.EndsWith($".locales.{id}.json", StringComparison.OrdinalIgnoreCase));
        if (resource is null)
        {
            return new Dictionary<string, string>();
        }

        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream is null)
        {
            return new Dictionary<string, string>();
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
        return data ?? new Dictionary<string, string>();
    }

    private static CultureInfo SafeCulture(string id)
    {
        try
        {
            return CultureInfo.GetCultureInfo(LanguageCatalog.Get(id).CultureName);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
