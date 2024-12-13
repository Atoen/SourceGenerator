using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace TextLocalizer;

[Generator]
internal class TextLocalizerGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AddTypes);

        var translationProviders = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TextLocalizer.TranslationProviderAttribute",
                predicate: static (_, _) => true,
                transform: static (ctx, _) => GetTextProviderAttributeData(ctx.SemanticModel, ctx.TargetNode)
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
                transform: static (ctx, _) => GetTranslationTableAttributeData(ctx.SemanticModel, ctx.TargetNode)
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
            static (productionContext, combined) => GenerateClasses(combined, productionContext)
        );
    }

    private static void GenerateClasses(ImmutableArray<TranslationsProviderData> providerData, SourceProductionContext context)
    {
        Dictionary<string, IndexedLocalizedText>? defaultDictionary = null;
        TranslationsProviderData? defaultProvider = null;

        var translationsAggregatedByFilename = new Dictionary<string, Dictionary<string, string>>();
        var nonDefaultTranslations = new Dictionary<TranslationsProviderData, Dictionary<string, LocalizedText>>();

        var builder = new StringBuilder();

        foreach (var data in providerData)
        {
            var parsed = FileParser.Parse(data.File);

            // Aggregating translations for generating xml docs
            foreach (var pair in parsed)
            {
                var (key, localizedText) = (pair.Key, pair.Value);
                if (!translationsAggregatedByFilename.TryGetValue(key, out var translations))
                {
                    translations = new Dictionary<string, string>();
                    translationsAggregatedByFilename[key] = translations;
                }

                translations[data.File.Name] = localizedText.Text;
            }

            if (data.ProviderAttributeData.IsDefault.Value)
            {
                defaultProvider = data;
                defaultDictionary = SourceGenerationHelper.CreateIndexedLocalizedTextDictionary(parsed);
            }
            else
            {
                nonDefaultTranslations[data] = parsed;
            }
        }

        if (defaultDictionary is null)
        {
            return;
        }

        foreach (var pair in nonDefaultTranslations)
        {
            var (data, translation) = (pair.Key, pair.Value);

            var indexedTranslations = IndexTranslationKeys(defaultDictionary, translation, data, context);
            var result = SourceGenerationHelper.GenerateProvider(builder, data.ProviderAttributeData, indexedTranslations);
            context.AddSource($"{data.ProviderAttributeData.ClassName}.g.cs", result);
        }

        if (defaultProvider is not { Table: { } tableData } provider)
        {
            return;
        }

        // Generate default provider and table with xml docs
        var providerClass = provider.ProviderAttributeData;
        var generatedDefaultProvider = SourceGenerationHelper.GenerateProvider(builder, providerClass, defaultDictionary);
        context.AddSource($"{providerClass.ClassName}.g.cs", generatedDefaultProvider);

        var generatedTable = SourceGenerationHelper.GenerateLocalizationTable(builder, tableData, defaultDictionary, translationsAggregatedByFilename);
        context.AddSource($"{tableData.ClassName}.g.cs", generatedTable);

        if (SyntaxFacts.IsValidIdentifier(tableData.IdClassName))
        {
            var generatedIdClass = SourceGenerationHelper.GenerateIdClass(builder, tableData, defaultDictionary, translationsAggregatedByFilename);
            context.AddSource($"{tableData.IdClassName.Value}.g.cs", generatedIdClass);
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
                    context.ReportUntranslatableKeyDiagnostic(providerData, key, localizedValue.LineNumber);
                }
                else
                {
                    indexedTranslations[key] = new IndexedLocalizedText(localizedValue.Text, defaultValue.Index, localizedValue.IsUntranslatable);
                }
            }
            else if (!defaultValue.IsUntranslatable)
            {
                context.ReportMissingKeyDiagnostic(providerData, key);
            }
        }

        foreach (var pair in localizedTable)
        {
            if (!defaultTable.ContainsKey(pair.Key))
            {
                context.ReportExtraKeyDiagnostic(providerData, pair.Key, pair.Value.LineNumber);
            }
        }

        return indexedTranslations;
    }

    private static ImmutableArray<SourceGeneratorData> CombineProvidersWithFiles(
        ImmutableArray<TextProviderAttributeData?> classes,
        ImmutableArray<TranslationsFileData> files)
    {
        var builder = ImmutableArray.CreateBuilder<SourceGeneratorData>();

        var fileLookup = files.ToDictionary(static x => x.Name);
        foreach (var classInfo in classes)
        {
            if (classInfo is not { } validClassInfo) continue;

            if (fileLookup.TryGetValue(validClassInfo.Filename.Value, out var matchingFile))
            {
                builder.Add(new SourceGeneratorData(validClassInfo, matchingFile));
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TranslationsProviderData> CreateProviderTableData(
        ImmutableArray<SourceGeneratorData> providerData,
        ImmutableArray<TranslationTableAttributeData?> tableData)
    {
        var table = tableData.Single();
        var builder = ImmutableArray.CreateBuilder<TranslationsProviderData>();

        foreach (var combinedProviderData in providerData)
        {
            builder.Add(new TranslationsProviderData(combinedProviderData.TextProviderAttributeData, combinedProviderData.TranslationsFile, table));
        }

        return builder.ToImmutable();
    }

    private static TextProviderAttributeData? GetTextProviderAttributeData(SemanticModel semanticModel, SyntaxNode classDeclarationSyntax)
    {
        return semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol
            ? SourceGenerationHelper.CreateTranslationProviderInfo(classSymbol)
            : null;
    }

    private static TranslationTableAttributeData? GetTranslationTableAttributeData(SemanticModel semanticModel, SyntaxNode classDeclarationSyntax)
    {
        return semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol classSymbol
            ? SourceGenerationHelper.CreateLocalizationTableData(classSymbol)
            : null;
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
    }
}
