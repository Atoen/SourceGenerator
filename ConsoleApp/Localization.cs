using System.Globalization;
using Generator.Generated;

namespace ConsoleApp;

public sealed record SupportedLanguage
{
    public const string EnglishTag = "en";
    public const string PolishTag = "pl";

    public static readonly SupportedLanguage English = new(EnglishTag);
    public static readonly SupportedLanguage Polish = new(PolishTag);

    public string Tag { get; }

    private SupportedLanguage(string tag)
    {
        Tag = tag;
    }

    public static SupportedLanguage? ParseLanguageCode(string languageCode) => languageCode switch
    {
        "en" => English,
        "pl" => Polish,
        _ => null
    };
}

[LocalizationTable(ProviderMethodName = nameof(Provider), TableName = "R")]
public partial class Localization
{
    private readonly Dictionary<SupportedLanguage, LocalizedTextProvider> _textProviders = new();

    public static SupportedLanguage DefaultLanguage { get; set; } = SupportedLanguage.English;

    public SupportedLanguage Language { get; private set; } = DefaultLanguage;
    public CultureInfo CultureInfo { get; private set; } = CultureInfo.GetCultureInfoByIetfLanguageTag(DefaultLanguage.Tag);

    public void SetLanguageByTag(string languageTag)
    {
        var parsed = SupportedLanguage.ParseLanguageCode(languageTag);
        SetLanguage(parsed ?? DefaultLanguage);
    }

    public void SetLanguage(SupportedLanguage language)
    {
        Language = language;
        CultureInfo = CultureInfo.GetCultureInfoByIetfLanguageTag(language.Tag);
    }

    private static LocalizedTextProvider CreateProvider(SupportedLanguage language) => language.Tag switch
    {
        SupportedLanguage.EnglishTag => new EnglishTextProvider(),
        SupportedLanguage.PolishTag => new PolishTextProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    private LocalizedTextProvider Provider => GetCurrentLanguageProvider();

    private LocalizedTextProvider GetCurrentLanguageProvider()
    {
        var language = Language;
        if (_textProviders.TryGetValue(language, out var provider))
        {
            return provider;
        }

        var newProvider = CreateProvider(language);
        _textProviders[language] = newProvider;

        return newProvider;
    }
}

