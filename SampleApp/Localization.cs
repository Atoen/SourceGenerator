using System.Globalization;
using TextLocalizer;
using TextLocalizer.Types;

namespace SampleApp;

[LocalizationTable(
    CurrentProviderAccessor = nameof(Provider),
    DefaultProviderAccessor = nameof(DefaultProvider),
    TableName = "R",
    GenerateDocs = true
    // GenereteIdTable = true
)]
public partial class Localization
{
    private readonly Dictionary<SupportedLanguage, ILocalizedTextProvider> _textProviders = new();

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

    private static ILocalizedTextProvider CreateProvider(SupportedLanguage language) => language.Tag switch
    {
        SupportedLanguage.EnglishTag => new EnglishTextProvider(),
        SupportedLanguage.PolishTag => new PolishTextProvider(),
        SupportedLanguage.GermanTag => new GermanTextProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };

    private ILocalizedTextProvider Provider => GetLanguageProvider(Language);
    private ILocalizedTextProvider DefaultProvider => GetLanguageProvider(DefaultLanguage);

    private ILocalizedTextProvider GetLanguageProvider(SupportedLanguage language)
    {
        if (_textProviders.TryGetValue(language, out var provider))
        {
            return provider;
        }

        var newProvider = CreateProvider(language);
        _textProviders[language] = newProvider;

        return newProvider;
    }
}
