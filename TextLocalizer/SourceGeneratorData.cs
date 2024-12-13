using Microsoft.CodeAnalysis.Text;

namespace TextLocalizer;

internal readonly record struct TranslationsProviderData
{
    public readonly TextProviderAttributeData ProviderAttributeData;
    public readonly TranslationsFileData File;
    public readonly TranslationTableAttributeData? Table;

    public TranslationsProviderData(TextProviderAttributeData providerAttributeData, TranslationsFileData file, TranslationTableAttributeData? table)
    {
        ProviderAttributeData = providerAttributeData;
        Table = table;
        File = file;
    }
}

internal readonly record struct SourceGeneratorData
{
    public readonly TextProviderAttributeData TextProviderAttributeData;
    public readonly TranslationsFileData TranslationsFile;

    public SourceGeneratorData(TextProviderAttributeData textProviderAttributeData, TranslationsFileData translationsFile)
    {
        TextProviderAttributeData = textProviderAttributeData;
        TranslationsFile = translationsFile;
    }
}

internal readonly record struct TranslationsFileData
{
    public readonly string Path;
    public readonly string Name;
    public readonly SourceText SourceText;

    public TranslationsFileData(string path, string name, SourceText sourceText)
    {
        Path = path;
        Name = name;
        SourceText = sourceText;
    }
}
