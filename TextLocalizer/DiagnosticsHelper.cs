using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace TextLocalizer;

internal static class DiagnosticsHelper
{
    public static void ReportMissingKeyDiagnostic(
        this SourceProductionContext context,
        TranslationsProviderData providerData,
        string key)
    {
        var location = Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan());

        var diagnostic = Diagnostic.Create(
            MissingKeyDescriptor,
            location,
            key, providerData.File.Name
        );

        context.ReportDiagnostic(diagnostic);
    }

    public static void ReportUntranslatableKeyDiagnostic(
        this SourceProductionContext context,
        TranslationsProviderData providerData,
        string key,
        int lineNumber)
    {
        var linePosition = new LinePosition(lineNumber, 0);
        var location = Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan(linePosition, linePosition));

        var diagnostic = Diagnostic.Create(
            UntranslatableKeyDescriptor,
            location,
            providerData.File.Name, key
        );

        context.ReportDiagnostic(diagnostic);
    }

    public static void ReportExtraKeyDiagnostic(
        this SourceProductionContext context,
        TranslationsProviderData providerData,
        string key,
        int lineNumber)
    {
        var linePosition = new LinePosition(lineNumber, 0);
        var location = Location.Create(providerData.File.Path, new TextSpan(), new LinePositionSpan(linePosition, linePosition));

        var diagnostic = Diagnostic.Create(
            ExtraKeyDescriptor,
            location,
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
}
