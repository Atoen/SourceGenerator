using System.Text;
using Microsoft.CodeAnalysis;

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
        #nullable enable
        
        namespace TextLocalizer
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class LocalizationTableAttribute : System.Attribute
            {
                public required string CurrentProviderAccessor { get; set; }
                
                public required string DefaultProviderAccessor { get; set; }
                
                public string TableName { get; set; } = "Table";
                
                public bool GenerateDocs { get; set; } = true;
                
                public string? IdClassName { get; set; }
            }
        }
        
        #nullable restore
        """;

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

    public static string GenerateProvider(
        StringBuilder builder,
        TextProviderAttributeData textProvider,
        Dictionary<string,
        IndexedLocalizedText> dictionary)
    {
        if (!textProvider.IsDefault)
        {
            builder.Append("#nullable enable\n\n");
        }

        builder
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

        var accessor = textProvider.IsDefault
            ? "public string this[int key] => _dictionary[key];\n"
            : "public string? this[int key] => _dictionary.GetValueOrDefault(key);\n";

        builder.Append("        ").Append(accessor);

        builder
            .Append("    }\n")
            .Append("}\n");

        if (!textProvider.IsDefault)
        {
           builder.Append("\n#nullable restore");
        }

        var result = builder.ToString();
        builder.Clear();

        return result;
    }

    public static string GenerateLocalizationTable(
        StringBuilder builder,
        TranslationTableAttributeData translationTable,
        Dictionary<string, IndexedLocalizedText> defaultDictionary,
        AllTranslationsData allTranslations)
    {
        builder
            .Append("#nullable enable\n\n")
            .Append("using TextLocalizer.Types;\n\n")
            .Append("namespace ").Append(translationTable.Namespace).Append('\n')
            .Append("{\n")
            .Append("    public partial class ").Append(translationTable.ClassName).Append('\n')
            .Append("    {\n")
            .Append("        private TextTable? _table;\n")
            .Append("        public TextTable ").Append(translationTable.TableName).Append(" => _table ??= new TextTable(this);\n\n")
            .Append("        public class TextTable(").Append(translationTable.ClassName).Append(" outer)\n")
            .Append("        {");

        if (!translationTable.GenerateDocs)
        {
            builder.Append('\n');
        }

        foreach (var pair in defaultDictionary)
        {
            var (key, localizedText) = (pair.Key, pair.Value);
            AppendTextProp(builder, key, localizedText, translationTable, allTranslations);
        }

        builder
            .Append("\n            public string this[StringResourceId id]")
            .Append(" => outer.").Append(translationTable.CurrentProviderAccessor)
            .Append("[id] ?? outer.")
            .Append(translationTable.DefaultProviderAccessor).Append("[id]!;\n");

        builder.Append("        }\n");

        builder
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        var result = builder.ToString();
        builder.Clear();

        return result;
    }

    private static void AppendTextProp(
        StringBuilder builder,
        string key,
        IndexedLocalizedText localizedText,
        TranslationTableAttributeData translationTable,
        AllTranslationsData allTranslations)
    {
        if (localizedText.IsUntranslatable)
        {
            if (translationTable.GenerateDocs)
            {
                builder.Append('\n');
                builder.Append("            /// <summary>\n");
                builder.Append("            /// <i>Marked as untranslatable</i><br/>\n");
                builder.Append("            /// ").Append(localizedText.Text).Append('\n');
                builder.Append("            /// </summary>\n");
            }

            builder
                .Append("            public string ").Append(key)
                .Append(" => outer.").Append(translationTable.DefaultProviderAccessor)
                .Append("[").Append(localizedText.Index).Append("]!;\n");
        }
        else
        {
            if (translationTable.GenerateDocs && allTranslations.TryGetValue(key, out var fileTranslations))
            {
                builder.Append('\n');
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
                .Append(" => outer.").Append(translationTable.CurrentProviderAccessor)
                .Append("[").Append(localizedText.Index).Append("] ?? outer.")
                .Append(translationTable.DefaultProviderAccessor).Append("[").Append(localizedText.Index).Append("]!;\n");
        }
    }

    public static string GenerateIdClass(
        StringBuilder builder,
        TranslationTableAttributeData translationTable,
        Dictionary<string, IndexedLocalizedText> defaultDictionary,
        AllTranslationsData allTranslations)
    {

        builder
            .Append("#nullable enable\n\n")
            .Append("using TextLocalizer.Types;\n\n")
            .Append("namespace ").Append(translationTable.Namespace).Append("\n")
            .Append("{\n")
            .Append("    public class ").Append(translationTable.IdClassName).Append('\n')
            .Append("    {");

        if (!translationTable.GenerateDocs)
        {
            builder.Append('\n');
        }

        foreach (var pair in defaultDictionary)
        {
            var (key, localizedText) = (pair.Key, pair.Value);
            AppendIdProp(builder, key, localizedText, translationTable, allTranslations);
        }

        builder
            .Append("    }\n")
            .Append("}\n\n")
            .Append("#nullable restore");

        var result = builder.ToString();
        builder.Clear();

        return result;
    }

    private static void AppendIdProp(
        StringBuilder builder,
        string key,
        IndexedLocalizedText localizedText,
        TranslationTableAttributeData translationTable,
        AllTranslationsData allTranslations)
    {
        if (translationTable.GenerateDocs)
        {
            if (localizedText.IsUntranslatable)
            {
                builder.Append('\n');
                builder.Append("        /// <summary>\n");
                builder.Append("        /// <i>Marked as untranslatable</i><br/>\n");
                builder.Append("        /// ").Append(localizedText.Text).Append('\n');
                builder.Append("        /// </summary>\n");
            }
            else if (allTranslations.TryGetValue(key, out var fileTranslations))
            {
                builder.Append('\n');
                builder.Append("        /// <summary>\n");
                builder.Append("        ///  <list type=\"table\">\n");
                builder.Append("        ///   <listheader>\n");
                builder.Append("        ///    <term>File</term>\n");
                builder.Append("        ///    <description>Translation</description>\n");
                builder.Append("        ///   </listheader>\n");

                foreach (var pair in fileTranslations)
                {
                    var (filename, translation) = (pair.Key, pair.Value);

                    builder.Append("        ///   <item>\n");
                    builder.Append("        ///    <term>").Append(filename).Append("</term>\n");
                    builder.Append("        ///    <description>").Append(translation).Append("</description>\n");
                    builder.Append("        ///   </item>\n");
                }

                builder.Append("        ///  </list>\n");
                builder.Append("        /// </summary>\n");
            }
        }

        builder.Append("        public static readonly StringResourceId ").Append(key).Append(" = new(").Append(localizedText.Index).Append(");\n");
    }

    public static TextProviderAttributeData? CreateTranslationProviderInfo(INamedTypeSymbol classSymbol)
    {
        var attributeData = GetAttributeData(classSymbol, TranslationProviderAttributeName);
        if (attributeData is null) return null;

        var @namespace = classSymbol.ContainingNamespace.ToString();
        var className = classSymbol.Name;

        return new TextProviderAttributeData(@namespace, className, attributeData);
    }

    public static TranslationTableAttributeData? CreateLocalizationTableData(INamedTypeSymbol classSymbol)
    {
        var attributeData = GetAttributeData(classSymbol, LocalizationTableAttributeName);
        if (attributeData is null) return null;

        var @namespace = classSymbol.ContainingNamespace.ToString();
        var className = classSymbol.Name;

        var data = new TranslationTableAttributeData(@namespace, className, attributeData);

        return data.IsValid() ? data : null;
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
