using Microsoft.CodeAnalysis.Text;

namespace Generator;

public readonly record struct TranslationProviderClassData
{
    public readonly string Namespace;
    public readonly string ClassName;
    public readonly string Filename;
    public readonly bool IsDefault;

    public TranslationProviderClassData(string @namespace, string className, string filename, bool isDefault)
    {
        Namespace = @namespace;
        ClassName = className;
        Filename = filename;
        IsDefault = isDefault;
    }
}

public readonly record struct TranslationsProviderData
{
    public readonly TranslationProviderClassData ProviderClass;
    public readonly TranslationsFileData File;
    public readonly LocalizationTableData? Table;

    public TranslationsProviderData(TranslationProviderClassData providerProviderClass, TranslationsFileData file, LocalizationTableData? table)
    {
        ProviderClass = providerProviderClass;
        Table = table;
        File = file;
    }
}

public readonly record struct TranslationsFileProviderData
{
    public readonly TranslationProviderClassData ProviderClass;
    public readonly TranslationsFileData TranslationsFile;

    public TranslationsFileProviderData(TranslationProviderClassData providerClass, TranslationsFileData translationsFile)
    {
        ProviderClass = providerClass;
        TranslationsFile = translationsFile;
    }
}

public readonly record struct TranslationsFileData
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

public readonly record struct LocalizationTableData
{
    public readonly string Namespace;
    public readonly string ClassName;
    public readonly string AccessMethodName;
    public readonly string TableName;

    public LocalizationTableData(string @namespace, string className, string accessMethodName, string tableName)
    {
        Namespace = @namespace;
        ClassName = className;
        AccessMethodName = accessMethodName;
        TableName = tableName;
    }
}
