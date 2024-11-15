using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(ctx =>
        {
            ctx.AddSource(
                "TranslationTableAttribute.g.cs",
                SourceText.From(SourceGenerationHelper.TableAttribute, Encoding.UTF8)
            );
        });

        var files = context.AdditionalTextsProvider
            .Where(static x => x.Path.EndsWith("home.json"))
            .Select(static (additionalText, cancellationToken) =>
            {
                var path = Path.GetFileNameWithoutExtension(additionalText.Path);
                var text = additionalText.GetText(cancellationToken);

                return (path, text);
            });

        var translationClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "Generator.Generated.TranslationTableAttribute",
                predicate: static (_, _) => true,
                transform: static (ctx, _) => GetTranslationClassInfo(ctx.SemanticModel, ctx.TargetNode)
            ).Where(static x => x is not null);

        // var translationClasses = context.SyntaxProvider
        //     .CreateSyntaxProvider(
        //         predicate: static (node, _) => IsClassWithAttribute(node),
        //         transform: static (context, _) => GetSemanticTarget(context)
        //     )
        //     .Where(static m => m is not null)
        //     .Collect();

        // var enumsToGenerate = context.SyntaxProvider
        //     .ForAttributeWithMetadataName(
        //         "Generator.Generated.EnumExtensionsAttribute",
        //         predicate: static (_, _) => true,
        //         transform: static (ctx, _) => GetEnumToGenerate(ctx.SemanticModel, ctx.TargetNode)
        //     ).Where(static m => m is not null);
        //
        // context.RegisterSourceOutput(
        //     enumsToGenerate,
        //     static (productionContext, source) => Execute(source, productionContext)
        // );
    }

    private static TranslationClassInfo? GetTranslationClassInfo(SemanticModel semanticModel, SyntaxNode classDeclarationSyntax)
    {
        if (semanticModel.GetDeclaredSymbol(classDeclarationSyntax) is not INamedTypeSymbol classSymbol)
        {
            return null;
        }
    }

    private static void GenerateTranslationClass(TranslationClassInfo? translationClassInfo, SourceProductionContext context)
    {
        if (translationClassInfo is not { } info) return;
    }

    // private static void Execute(EnumToGenerate? enumToGenerate, SourceProductionContext context)
    // {
    //     if (enumToGenerate is not { } value) return;
    //
    //     var result = SourceGenerationHelper.GenerateExtensionClass(value);
    //     context.AddSource($"EnumExtensions.{value.Name}.g.cs", SourceText.From(result, Encoding.UTF8));
    // }
    //
    // private static EnumToGenerate? GetEnumToGenerate(SemanticModel semanticModel, SyntaxNode enumDeclarationSyntax)
    // {
    //     if (semanticModel.GetDeclaredSymbol(enumDeclarationSyntax) is not INamedTypeSymbol enumSymbol)
    //     {
    //         return null;
    //     }
    //
    //     var enumName = enumSymbol.ToString();
    //     var enumMembers = enumSymbol.GetMembers();
    //
    //     var members = new List<string>(enumMembers.Length);
    //
    //     foreach (var enumMember in enumMembers)
    //     {
    //         if (enumMember is IFieldSymbol { ConstantValue: not null })
    //         {
    //             members.Add(enumMember.Name);
    //         }
    //     }
    //
    //     return new EnumToGenerate(enumName, members);
    // }
}
