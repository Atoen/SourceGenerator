using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(AddTypes);

        var translationProviders = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Generator.Generated.TranslationProviderAttribute",
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
                "Generator.Generated.LocalizationTableAttribute",
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
        Dictionary<string, LocalizedText>? defaultDictionary = null;

        var nonDefaultTranslations = new Dictionary<TranslationsProviderData, Dictionary<string, LocalizedText>>();

        foreach (var data in providerData)
        {
            if (data.ProviderClass.IsDefault)
            {
                defaultDictionary = ParseFileData(data.File);
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
            var (translation, data) = (pair.Value, pair.Key);

            CompareKeys(defaultDictionary, translation, data, context);

            var result = SourceGenerationHelper.GenerateProvider(data.ProviderClass, translation);
            context.AddSource($"{data.ProviderClass.ClassName}.g.cs", result);
        }
    }

    private static void CompareKeys(
        IDictionary<string, LocalizedText> primary,
        IDictionary<string, LocalizedText> toCompare,
        TranslationsProviderData providerData,
        SourceProductionContext context)
    {
        foreach (var pair in primary)
        {
            if (!toCompare.ContainsKey(pair.Key) && !pair.Value.Untranslatable)
            {
                var diagnostic = Diagnostic.Create(
                MissingKeyDescriptor,
                Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan()),
                pair.Key, providerData.File.Name);
                context.ReportDiagnostic(diagnostic);
            }
            else if (pair.Value.Untranslatable)
            {
                var line = pair.Value.LineNumber;
                var linePosition = new LinePosition(line, 0);

                var diagnostic = Diagnostic.Create(
                UntranslatableKeyDescriptor,
                Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan(linePosition, linePosition)),
                providerData.File.Name, pair.Key);
                context.ReportDiagnostic(diagnostic);
            }
        }

        foreach (var pair in toCompare)
        {
            if (!primary.ContainsKey(pair.Key) && !pair.Value.Untranslatable)
            {
                var line = pair.Value.LineNumber;
                var linePosition = new LinePosition(line, 0);

                var diagnostic = Diagnostic.Create(
                ExtraKeyDescriptor,
                Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan(linePosition, linePosition)),
                providerData.File.Name, pair.Key);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static readonly DiagnosticDescriptor MissingKeyDescriptor = new DiagnosticDescriptor(
        id: "DIAG001",
        title: "Missing Item in Dictionary",
        messageFormat: "The key '{0}' is missing its translation in {1} file",
        category: "DictionaryComparison",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor ExtraKeyDescriptor = new DiagnosticDescriptor(
        id: "DIAG002",
        title: "Extra Item in Dictionary",
        messageFormat: "File {0} contains key '{1}', which is not present in the main translations file",
        category: "DictionaryComparison",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static readonly DiagnosticDescriptor UntranslatableKeyDescriptor = new DiagnosticDescriptor(
        id: "DIAG003",
        title: "Extra Item in Dictionary",
        messageFormat: "File {0} contains key '{1}', which is marked as untranslatable",
        category: "DictionaryComparison",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    private static ImmutableArray<TranslationsFileProviderData> CombineProvidersWithFiles(
        ImmutableArray<TranslationProviderClassData?> classes,
        ImmutableArray<TranslationsFileData> files)
    {
        var builder = ImmutableArray.CreateBuilder<TranslationsFileProviderData>();

        var fileLookup = files.ToDictionary(static x => x.Name);
        foreach (var classInfo in classes)
        {
            if (classInfo is not { } validClassInfo) continue;

            if (fileLookup.TryGetValue(validClassInfo.Filename, out var matchingFile))
            {
                builder.Add(new TranslationsFileProviderData(validClassInfo, matchingFile));
            }
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<TranslationsProviderData> CreateProviderTableData(
        ImmutableArray<TranslationsFileProviderData> providerData,
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

            var untranslatable = commentPart.Equals("untranslatable".AsSpan(), StringComparison.OrdinalIgnoreCase);

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
            "LocalizedTextProvider.g.cs",
            SourceText.From(SourceGenerationHelper.ProviderClass, Encoding.UTF8)
        );
    }
}
