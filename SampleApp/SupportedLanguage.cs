namespace SampleApp;

public sealed record SupportedLanguage
{
    public const string EnglishTag = "en";
    public const string PolishTag = "pl";
    public const string GermanTag = "de";

    public static readonly SupportedLanguage English = new(EnglishTag);
    public static readonly SupportedLanguage Polish = new(PolishTag);
    public static readonly SupportedLanguage German = new(GermanTag);

    public string Tag { get; }

    private SupportedLanguage(string tag)
    {
        Tag = tag;
    }

    public static SupportedLanguage? ParseLanguageCode(string languageCode) => languageCode switch
    {
        EnglishTag => English,
        PolishTag => Polish,
        GermanTag => German,
        _ => null
    };
}
