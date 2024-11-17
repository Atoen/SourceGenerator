using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Generator;

public static class SourceGenerationHelper
{
    private const string TranslationProviderAttributeName = "TranslationProviderAttribute";
    private const string LocalizationTableAttributeName = "LocalizationTableAttribute";

    public const string ProviderAttribute =
        """
        #nullable enable

        namespace Generator.Generated
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
        namespace Generator.Generated
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class LocalizationTableAttribute : System.Attribute
            {
                public required string ProviderMethodName { get; set; }
                
                public string TableName { get; set; } = "Table";
            }
        }
        """;

    public const string ProviderClass =
        """
        namespace Generator.Generated
        {
            public abstract class LocalizedTextProvider(Dictionary<string, string> dictionary)
            {
                public string this[string key] => dictionary.TryGetValue(key, out var value) ? value : key;
            }
        }
        """;

    public static bool IsValidNameIdentifier(string identifier)
    {
        return SyntaxFacts.IsValidIdentifier(identifier);
    }

    public static string GenerateProvider(TranslationProviderClassData classData, Dictionary<string, LocalizedText> dictionary)
    {
        var builder = new StringBuilder()
            .Append("using Generator.Generated;\n\n")
            .Append("namespace ").Append(classData.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(classData.ClassName).Append("() : LocalizedTextProvider(Dictionary)\n")
            .Append("    {\n")
            .Append("        private static Dictionary<string, string> Dictionary => new()\n")
            .Append("        {\n");

        foreach (var pair in dictionary)
        {
            var (key, value) = (pair.Key, pair.Value);
            builder.Append("            { \"").Append(key).Append("\", \"").Append(value.Text);

            builder.Append(value.Untranslatable ? "\" }, // Untranslatable \n" : "\" },\n");
        }

        builder
            .Append("        };\n")
            .Append("    }\n")
            .Append("}\n");

        return builder.ToString();
    }

    public static string GenerateLocalizationTable(LocalizationTableData data, Dictionary<string, LocalizedText> dictionary)
    {
        var builder = new StringBuilder()
            .Append("#nullable enable\n\n")
            .Append("using Generator.Generated;\n\n")
            .Append("namespace ").Append(data.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(data.ClassName).Append('\n')
            .Append("    {\n")
            .Append("        private TextTable? _table;\n")
            .Append("        public TextTable ").Append(data.TableName).Append(" => _table ??= new TextTable(this);\n\n")
            .Append("        public class TextTable(").Append(data.ClassName).Append(" outer)\n")
            .Append("        {\n");

        foreach (var key in dictionary.Keys)
        {
            builder.Append("            public string ").Append(key).Append(" => outer.").Append(data.AccessMethodName).Append("[\"").Append(key).Append("\"];\n");
        }

        builder.Append("        }\n")
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        return builder.ToString();
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
        var providerMethodName = "";
        var tableName = "";

        foreach (var namedArgument in attributeData.NamedArguments)
        {
            if (namedArgument is { Key: "ProviderMethodName", Value.Value: string providerMethodNameValue })
            {
                providerMethodName = providerMethodNameValue;
            }

            if (namedArgument is { Key: "TableName", Value.Value: string tableNameValue })
            {
                tableName = tableNameValue;
            }
        }

        if (!IsValidNameIdentifier(providerMethodName) || !IsValidNameIdentifier(tableName))
        {
            return null;
        }

        return new LocalizationTableData(@namespace, className, providerMethodName, tableName);
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
