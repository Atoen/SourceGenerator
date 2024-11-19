using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TextLocalizer;

[Generator]
public class TextLocalizerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AddTypes);

        var translationProviders = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TextLocalizer.TranslationProviderAttribute",
                predicate: static (_, _) => true,
                transform: static (ctx, _) => GetTranslationProviderInfo(ctx.SemanticModel, ctx.TargetNode)
            )
            .Where(static x => x is not null)
            .Collect();

        var additionalFiles = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith(".yml") || x.Path.EndsWith(".yaml"))
            .Select(static (additionalText, cancellationToken) =>
            {
                var path = additionalText.Path;
                var name = Path.GetFileName(path);
                var text = additionalText.GetText(cancellationToken);

                return (path, name, text);
            })
            .Where(static tuple => tuple.text is not null)
            .Select(static (tuple, _) => new TranslationsFileData(tuple.path, tuple.name, tuple.text!))
            .Collect();

        var localizationTables = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TextLocalizer.LocalizationTableAttribute",
                predicate: static (_, _) => true,
                transform: static (ctx, _) => GetLocalizationTableData(ctx.SemanticModel, ctx.TargetNode)
            )
            .Where(static x => x is not null)
            .Collect();

        var combinedData = translationProviders
            .Combine(additionalFiles)
            .Select(static (combined, _) =>
            {
                var (providers, files) = combined;
                return CombineProvidersWithFiles(providers, files);
            })
            .Combine(localizationTables)
            .SelectMany(static (combined, _) =>
            {
                var (combinedProviderData, localizationTable) = combined;
                return CreateProviderTableData(combinedProviderData, localizationTable);
            })
            .Collect();

        context.RegisterSourceOutput(
            combinedData,
            static (productionContext, combined) => GenerateTranslationClass(combined, productionContext)
        );
    }

    private static void GenerateTranslationClass(ImmutableArray<TranslationsProviderData> providerData, SourceProductionContext context)
    {
        Dictionary<string, IndexedLocalizedText>? defaultDictionary = null;

        var nonDefaultTranslations = new Dictionary<TranslationsProviderData, Dictionary<string, LocalizedText>>();

        foreach (var data in providerData)
        {
            if (data.ProviderClass.IsDefault)
            {
                var defaultParsed = ParseFileData(data.File);
                defaultDictionary = SourceGenerationHelper.CreateIndexedLocalizedTextDictionary(defaultParsed);

                if (data.Table is { } table)
                {
                    var emptyTable = SourceGenerationHelper.GenerateLocalizationTable(table, defaultDictionary);
                    context.AddSource($"{table.ClassName}.g.cs", emptyTable);
                }

                var result = SourceGenerationHelper.GenerateProvider(data.ProviderClass, defaultDictionary);
                context.AddSource($"{data.ProviderClass.ClassName}.g.cs", result);

                continue;
            }

            nonDefaultTranslations[data] = ParseFileData(data.File);
        }

        if (defaultDictionary is null)
        {
            return;
        }

        foreach (var pair in nonDefaultTranslations)
        {
            var (data, translation) = (pair.Key, pair.Value);

            var indexedTranslations = IndexTranslationKeys(defaultDictionary, translation, data, context);
            var result = SourceGenerationHelper.GenerateProvider(data.ProviderClass, indexedTranslations);
            context.AddSource($"{data.ProviderClass.ClassName}.g.cs", result);
        }
    }

    private static Dictionary<string, IndexedLocalizedText> IndexTranslationKeys(
        Dictionary<string, IndexedLocalizedText> defaultTable,
        Dictionary<string, LocalizedText> localizedTable,
        TranslationsProviderData providerData,
        SourceProductionContext context)
    {
        var indexedTranslations = new Dictionary<string, IndexedLocalizedText>();

        foreach (var pair in defaultTable)
        {
            var (key, defaultValue) = (pair.Key, pair.Value);

            if (localizedTable.TryGetValue(key, out var localizedValue))
            {
                if (defaultValue.IsUntranslatable)
                {
                    ReportUntranslatableKeyDiagnostic(context, providerData, key, localizedValue.LineNumber);
                }
                else
                {
                    indexedTranslations[key] = new IndexedLocalizedText(localizedValue.Text, defaultValue.Index, localizedValue.IsUntranslatable);
                }
            }
            else if (!defaultValue.IsUntranslatable)
            {
                ReportMissingKeyDiagnostic(context, providerData, key);
            }
        }

        foreach (var pair in localizedTable)
        {
            if (!defaultTable.ContainsKey(pair.Key))
            {
                ReportExtraKeyDiagnostic(context, providerData, pair.Key, pair.Value.LineNumber);
            }
        }

        return indexedTranslations;
    }

    private static void ReportMissingKeyDiagnostic(
        SourceProductionContext context,
        TranslationsProviderData providerData,
        string key)
    {
        var diagnostic = Diagnostic.Create(
            MissingKeyDescriptor,
            Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan()),
            key, providerData.File.Name
        );
        context.ReportDiagnostic(diagnostic);
    }

    private static void ReportUntranslatableKeyDiagnostic(
        SourceProductionContext context,
        TranslationsProviderData providerData,
        string key,
        int lineNumber)
    {
        var linePosition = new LinePosition(lineNumber, 0);
        var diagnostic = Diagnostic.Create(
            UntranslatableKeyDescriptor,
            Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan(linePosition, linePosition)),
            providerData.File.Name, key
        );
        context.ReportDiagnostic(diagnostic);
    }

    private static void ReportExtraKeyDiagnostic(
        SourceProductionContext context,
        TranslationsProviderData providerData,
        string key,
        int lineNumber)
    {
        var linePosition = new LinePosition(lineNumber, 0);
        var diagnostic = Diagnostic.Create(
            ExtraKeyDescriptor,
            Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan(linePosition, linePosition)),
            providerData.File.Name, key
        );
        context.ReportDiagnostic(diagnostic);
    }

    private static readonly DiagnosticDescriptor MissingKeyDescriptor = new(
        id: "TL001",
        title: "Missing item in dictionary",
        messageFormat: "The key '{0}' is missing its translation in {1} file",
        category: "DictionaryComparison",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor ExtraKeyDescriptor = new(
        id: "TL002",
        title: "Extra item in dictionary",
        messageFormat: "File {0} contains key '{1}', which is not present in the main translations file",
        category: "DictionaryComparison",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor UntranslatableKeyDescriptor = new(
        id: "TL003",
        title: "Untranslatable key is localized",
        messageFormat: "File {0} contains key '{1}', which is marked as untranslatable",
        category: "DictionaryComparison",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static ImmutableArray<SourceGeneratorData> CombineProvidersWithFiles(
        ImmutableArray<TranslationProviderClassData?> classes,
        ImmutableArray<TranslationsFileData> files)
    {
        var builder = ImmutableArray.CreateBuilder<SourceGeneratorData>();

        var fileLookup = files.ToDictionary(static x => x.Name);
        foreach (var classInfo in classes)
        {
            if (classInfo is not { } validClassInfo) continue;

            if (fileLookup.TryGetValue(validClassInfo.Filename, out var matchingFile))
            {
                builder.Add(new SourceGeneratorData(validClassInfo, matchingFile));
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TranslationsProviderData> CreateProviderTableData(
        ImmutableArray<SourceGeneratorData> providerData,
        ImmutableArray<LocalizationTableData?> tableData)
    {
        var table = tableData.Single();
        var builder = ImmutableArray.CreateBuilder<TranslationsProviderData>();

        foreach (var combinedProviderData in providerData)
        {
            builder.Add(new TranslationsProviderData(combinedProviderData.ProviderClass, combinedProviderData.TranslationsFile, table));
        }

        return builder.ToImmutable();
    }

    private static TranslationProviderClassData? GetTranslationProviderInfo(SemanticModel semanticModel, SyntaxNode classDeclarationSyntax)
    {
        return semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol
            ? SourceGenerationHelper.CreateTranslationProviderInfo(classSymbol)
            : null;
    }

    private static LocalizationTableData? GetLocalizationTableData(SemanticModel semanticModel, SyntaxNode classDeclarationSyntax)
    {
        return semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol
            ? SourceGenerationHelper.CreateLocalizationTableData(classSymbol)
            : null;
    }

    private static Dictionary<string, LocalizedText> ParseFileData(TranslationsFileData fileData)
    {
        var dictionary = new Dictionary<string, LocalizedText>();
        var untranslatableSpan = "untranslatable".AsSpan();

        for (var i = 0; i < fileData.SourceText.Lines.Count; i++)
        {
            var line = fileData.SourceText.Lines[i];

            var spanLine = line.ToString().AsSpan().Trim();
            if (spanLine.IsEmpty || spanLine[0] == '#')
            {
                continue;
            }

            var colonIndex = spanLine.IndexOf(':');
            if (colonIndex == -1)
            {
                continue;
            }

            ReadOnlySpan<char> keyValuePart;
            var commentPart = ReadOnlySpan<char>.Empty;
            var commentIndex = spanLine.IndexOf('#');

            if (commentIndex != -1)
            {
                keyValuePart = spanLine.Slice(0, commentIndex).Trim();
                commentPart = spanLine.Slice(commentIndex + 1).Trim();
            }
            else
            {
                keyValuePart = spanLine;
            }

            var key = keyValuePart.Slice(0, colonIndex).TrimEnd();
            var value = keyValuePart.Slice(colonIndex + 1).TrimStart();

            var untranslatable = commentPart.Equals(untranslatableSpan, StringComparison.OrdinalIgnoreCase);

            dictionary[key.ToString()] = new LocalizedText(value.ToString(), i, untranslatable);
        }

        return dictionary;
    }

    private static void AddTypes(IncrementalGeneratorPostInitializationContext context)
    {
        context.AddSource(
            "TranslationProviderAttribute.g.cs",
            SourceText.From(SourceGenerationHelper.ProviderAttribute, Encoding.UTF8)
        );

        context.AddSource(
            "LocalizationTableAttribute.g.cs",
            SourceText.From(SourceGenerationHelper.LocalizationTableAttribute, Encoding.UTF8)
        );

        context.AddSource(
            "ILocalizedTextProvider.g.cs",
            SourceText.From(SourceGenerationHelper.ProviderInterface, Encoding.UTF8)
        );
    }
}
