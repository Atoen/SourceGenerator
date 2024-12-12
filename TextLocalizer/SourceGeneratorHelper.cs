using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TextLocalizer;

using AllTranslationsData = Dictionary<string, Dictionary<string, string>>;

internal static class SourceGenerationHelper
{
    private const string TranslationProviderAttributeName = "TranslationProviderAttribute";
    private const string LocalizationTableAttributeName = "LocalizationTableAttribute";

    public const string ProviderAttribute =
        """
        namespace TextLocalizer
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TranslationProviderAttribute : System.Attribute
            {
                public required string Filename { get; set; }
                
                public bool IsDefault { get; set; }
            }
        }
        """;

    public const string LocalizationTableAttribute =
        """
        namespace TextLocalizer
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class LocalizationTableAttribute : System.Attribute
            {
                public required string CurrentProviderAccessor { get; set; }
                
                public required string DefaultProviderAccessor { get; set; }
                
                public string TableName { get; set; } = "Table";
                
                public bool GenerateDocs { get; set; } = true;
                
                public bool GenereteIdTable { get; set; } = true;
            }
        }
        """;


    public static bool IsValidNameIdentifier(string identifier)
    {
        return SyntaxFacts.IsValidIdentifier(identifier);
    }

    public static Dictionary<string, IndexedLocalizedText> CreateIndexedLocalizedTextDictionary(Dictionary<string, LocalizedText> parsedLocalizedTexts)
    {
        var indexedDictionary = new Dictionary<string, IndexedLocalizedText>();
        var index = 1;

        foreach (var pair in parsedLocalizedTexts)
        {
            var (key, parsed) = (pair.Key, pair.Value);
            var indexedText = new IndexedLocalizedText(
                parsed.Text,
                index,
                parsed.IsUntranslatable
            );

            indexedDictionary[key] = indexedText;
            index++;
        }

        return indexedDictionary;
    }

    public static string GenerateProvider(TextProviderAttributeData textProvider, Dictionary<string, IndexedLocalizedText> dictionary)
    {
        var builder = new StringBuilder()
            .Append("#nullable enable\n\n")
            .Append("using TextLocalizer.Types;\n\n")
            .Append("namespace ").Append(textProvider.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(textProvider.ClassName).Append(" : ILocalizedTextProvider\n")
            .Append("    {\n")
            .Append("        private readonly Dictionary<int, string> _dictionary = new()\n")
            .Append("        {\n");

        foreach (var value in dictionary.Values)
        {
            builder.Append("            { ").Append(value.Index).Append(", \"").Append(value.Text);
            builder.Append(value.IsUntranslatable ? "\" }, // Untranslatable \n" : "\" },\n");
        }

        builder.Append("        };\n\n");

        var accessor = textProvider.IsDefault.Value
            ? "public string this[int key] => _dictionary[key];\n"
            : "public string? this[int key] => _dictionary.GetValueOrDefault(key);\n";

        builder.Append("        ").Append(accessor);

        builder
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        return builder.ToString();
    }

    public static string GenerateLocalizationTable(
        TranslationTableAttributeData translationTable,
        Dictionary<string, IndexedLocalizedText> defaultDictionary,
        AllTranslationsData allTranslations)
    {
        var builder = new StringBuilder()
            .Append("#nullable enable\n\n")
            .Append("using TextLocalizer.Types;\n\n")
            .Append("namespace ").Append(translationTable.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(translationTable.ClassName).Append('\n')
            .Append("    {\n")
            .Append("        private TextTable? _table;\n")
            .Append("        public TextTable ").Append(translationTable.TableName).Append(" => _table ??= new TextTable(this);\n\n")
            .Append("        public class TextTable(").Append(translationTable.ClassName).Append(" outer)\n")
            .Append("        {\n");

        foreach (var pair in defaultDictionary)
        {
            var (key, localizedText) = (pair.Key, pair.Value);
            AppendTextTableProperty(builder, key, localizedText, translationTable, allTranslations);
        }

        builder.Append("        }\n")
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        return builder.ToString();
    }

    private static void AppendTextTableProperty(
        StringBuilder builder,
        string key,
        IndexedLocalizedText localizedText,
        TranslationTableAttributeData translationTable,
        AllTranslationsData allTranslations)
    {

        if (localizedText.IsUntranslatable)
        {
            if (translationTable.GenerateDocs.Value)
            {
                builder.Append('\n');
                builder.Append("            /// <summary>\n");
                builder.Append("            /// <i>Marked as untranslatable</i><br/>\n");
                builder.Append("            /// ").Append(localizedText.Text).Append('\n');
                builder.Append("            /// </summary>\n");
            }

            builder
                .Append("            public string ").Append(key)
                .Append(" => outer.").Append(translationTable.DefaultProviderAccessor.Value)
                .Append("[").Append(localizedText.Index).Append("]!;\n");
        }
        else
        {
            if (translationTable.GenerateDocs.Value && allTranslations.TryGetValue(key, out var fileTranslations))
            {
                builder.Append("            /// <summary>\n");
                builder.Append("            ///  <list type=\"table\">\n");
                builder.Append("            ///   <listheader>\n");
                builder.Append("            ///    <term>File</term>\n");
                builder.Append("            ///    <description>Translation</description>\n");
                builder.Append("            ///   </listheader>\n");

                foreach (var pair in fileTranslations)
                {
                    var (filename, translation) = (pair.Key, pair.Value);

                    builder.Append("            ///   <item>\n");
                    builder.Append("            ///    <term>").Append(filename).Append("</term>\n");
                    builder.Append("            ///    <description>").Append(translation).Append("</description>\n");
                    builder.Append("            ///   </item>\n");
                }

                builder.Append("            ///  </list>\n");
                builder.Append("            /// </summary>\n");
            }

            builder
                .Append("            public string ").Append(key)
                .Append(" => outer.").Append(translationTable.CurrentProviderAccessor.Value)
                .Append("[").Append(localizedText.Index).Append("] ?? outer.")
                .Append(translationTable.DefaultProviderAccessor.Value).Append("[").Append(localizedText.Index).Append("]!;\n");
        }
    }

    public static TextProviderAttributeData? CreateTranslationProviderInfo(INamedTypeSymbol classSymbol)
    {
        var attributeData = GetAttributeData(classSymbol, TranslationProviderAttributeName);
        if (attributeData is null) return null;

        var @namespace = classSymbol.ContainingNamespace.ToString();
        var className = classSymbol.Name;

        return new TextProviderAttributeData(@namespace, className, attributeData);



        // var filename = "";
        // var isDefault = false;

        // foreach (var namedArgument in attributeData.NamedArguments)
        // {
        //     if (namedArgument is { Key: "Filename", Value.Value: string filenameValue })
        //     {
        //         filename = filenameValue;
        //     }
        //
        //     if (namedArgument is { Key: "IsDefault", Value.Value: bool isDefaultValue })
        //     {
        //         isDefault = isDefaultValue;
        //     }
        // }

        // return new TranslationProviderClassData(@namespace, className, filename, isDefault);
    }

    public static TranslationTableAttributeData? CreateLocalizationTableData(INamedTypeSymbol classSymbol)
    {
        var attributeData = GetAttributeData(classSymbol, LocalizationTableAttributeName);
        if (attributeData is null) return null;

        var @namespace = classSymbol.ContainingNamespace.ToString();
        var className = classSymbol.Name;

        return new TranslationTableAttributeData(@namespace, className, attributeData);

        // var currentProviderAccessor = "";
        // var defaultProviderAccessor = "";
        // var tableName = "Table";
        // var generateDocs = true;
        // var generateIdTable = true;

        // foreach (var namedArgument in attributeData.NamedArguments)
        // {
        //     if (namedArgument is { Key: "CurrentProviderAccessor", Value.Value: string currentProviderAccessorValue })
        //     {
        //         currentProviderAccessor = currentProviderAccessorValue;
        //     }
        //
        //     if (namedArgument is { Key: "DefaultProviderAccessor", Value.Value: string defaultProviderAccessorValue })
        //     {
        //         defaultProviderAccessor = defaultProviderAccessorValue;
        //     }
        //
        //     if (namedArgument is { Key: "TableName", Value.Value: string tableNameValue })
        //     {
        //         tableName = tableNameValue;
        //     }
        //
        //     if (namedArgument is { Key: "GenerateDocs", Value.Value: bool generateDocsValue })
        //     {
        //         generateDocs = generateDocsValue;
        //     }
        //
        //     if (namedArgument is { Key: "GenerateDocs", Value.Value: bool generateIdTableValue })
        //     {
        //         generateIdTable = generateIdTableValue;
        //     }
        // }
        //
        // if (!IsValidNameIdentifier(currentProviderAccessor) || !IsValidNameIdentifier(defaultProviderAccessor) || !IsValidNameIdentifier(tableName))
        // {
        //     return null;
        // }

        // return new LocalizationTableData(@namespace, className, currentProviderAccessor, defaultProviderAccessor, tableName, generateDocs);
    }

    private static AttributeData? GetAttributeData(INamedTypeSymbol classSymbol, string attributeName)
    {
        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == attributeName)
            {
                return attribute;
            }
        }

        return null;
    }
}
