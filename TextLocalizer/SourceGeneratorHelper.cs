using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TextLocalizer;

public static class SourceGenerationHelper
{
    private const string TranslationProviderAttributeName = "TranslationProviderAttribute";
    private const string LocalizationTableAttributeName = "LocalizationTableAttribute";

    public const string ProviderAttribute =
        """
        #nullable enable

        namespace TextLocalizer
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TranslationProviderAttribute : System.Attribute
            {
                public required string Filename { get; set; }
                
                public bool IsDefault { get; set; }
                
                public string? Directory { get; set; }
            }
        }

        #nullable restore
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
            }
        }
        """;

    public const string ProviderInterface =
        """
        #nullable enable
        
        namespace TextLocalizer
        {
            public interface ILocalizedTextProvider
            {
                string? this[int key] { get; }
            }
        }
        
        #nullable restore
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

    public static string GenerateProvider(TranslationProviderClassData classData, Dictionary<string, IndexedLocalizedText> dictionary)
    {
        var builder = new StringBuilder()
            .Append("#nullable enable\n\n")
            .Append("using TextLocalizer;\n\n")
            .Append("namespace ").Append(classData.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(classData.ClassName).Append(" : ILocalizedTextProvider\n")
            .Append("    {\n")
            .Append("        private readonly Dictionary<int, string> _dictionary = new()\n")
            .Append("        {\n");

        foreach (var value in dictionary.Values)
        {
            builder.Append("            { ").Append(value.Index).Append(", \"").Append(value.Text);
            builder.Append(value.IsUntranslatable ? "\" }, // Untranslatable \n" : "\" },\n");
        }

        builder.Append("        };\n\n");

        var accessor = classData.IsDefault
            ? "public string this[int key] => _dictionary[key];\n"
            : "public string? this[int key] => _dictionary.GetValueOrDefault(key);\n";

        builder.Append("        ").Append(accessor);

        builder
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        return builder.ToString();
    }

    public static string GenerateLocalizationTable(LocalizationTableData data, Dictionary<string, IndexedLocalizedText> dictionary)
    {
        var builder = new StringBuilder()
            .Append("#nullable enable\n\n")
            .Append("using TextLocalizer;\n\n")
            .Append("namespace ").Append(data.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(data.ClassName).Append('\n')
            .Append("    {\n")
            .Append("        private TextTable? _table;\n")
            .Append("        public TextTable ").Append(data.TableName).Append(" => _table ??= new TextTable(this);\n\n")
            .Append("        public class TextTable(").Append(data.ClassName).Append(" outer)\n")
            .Append("        {\n");

        foreach (var pair in dictionary)
        {
            var (key, localizedText) = (pair.Key, pair.Value);
            AppendTextTableProperty(builder, key, localizedText, data);
        }

        builder.Append("        }\n")
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        return builder.ToString();
    }

    private static void AppendTextTableProperty(StringBuilder builder, string key, IndexedLocalizedText localizedText, LocalizationTableData data)
    {
        if (localizedText.IsUntranslatable)
        {
            builder
                .Append("            public string ").Append(key)
                .Append(" => outer.").Append(data.DefaultProviderAccessor)
                .Append("[").Append(localizedText.Index).Append("]!;\n");
        }
        else
        {
            builder
                .Append("            public string ").Append(key)
                .Append(" => outer.").Append(data.CurrentProviderAccessor)
                .Append("[").Append(localizedText.Index).Append("] ?? outer.")
                .Append(data.DefaultProviderAccessor).Append("[").Append(localizedText.Index).Append("]!;\n");
        }
    }

    public static TranslationProviderClassData? CreateTranslationProviderInfo(INamedTypeSymbol classSymbol)
    {
        var attributeData = GetAttributeData(classSymbol, TranslationProviderAttributeName);
        if (attributeData is null) return null;

        var @namespace = classSymbol.ContainingNamespace.Name;
        var className = classSymbol.Name;
        var filename = "";
        var isDefault = false;

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument is { Key: "Filename", Value.Value: string filenameValue })
            {
                filename = filenameValue;
            }

            if (namedArgument is { Key: "IsDefault", Value.Value: bool isDefaultValue })
            {
                isDefault = isDefaultValue;
            }
        }

        return new TranslationProviderClassData(@namespace, className, filename, isDefault);
    }

    public static LocalizationTableData? CreateLocalizationTableData(INamedTypeSymbol classSymbol)
    {
        var attributeData = GetAttributeData(classSymbol, LocalizationTableAttributeName);
        if (attributeData is null) return null;

        var @namespace = classSymbol.ContainingNamespace.Name;
        var className = classSymbol.Name;
        var currentProviderAccessor = "";
        var defaultProviderAccessor = "";
        var tableName = "";

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument is { Key: "CurrentProviderAccessor", Value.Value: string currentProviderAccessorValue })
            {
                currentProviderAccessor = currentProviderAccessorValue;
            }
            
            if (namedArgument is { Key: "DefaultProviderAccessor", Value.Value: string defaultProviderAccessorValue })
            {
                defaultProviderAccessor = defaultProviderAccessorValue;
            }

            if (namedArgument is { Key: "TableName", Value.Value: string tableNameValue })
            {
                tableName = tableNameValue;
            }
        }

        if (!IsValidNameIdentifier(currentProviderAccessor) || !IsValidNameIdentifier(defaultProviderAccessor) || !IsValidNameIdentifier(tableName))
        {
            return null;
        }

        return new LocalizationTableData(@namespace, className, currentProviderAccessor, defaultProviderAccessor, tableName);
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
