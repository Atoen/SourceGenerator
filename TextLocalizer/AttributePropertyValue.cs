using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace TextLocalizer;

internal record struct AttributePropertyValue<T>
{
    public string Name { get; }
    public T DefaultValue { get; }

    public bool Read { get; private set; }
    public T Value => _value ?? DefaultValue;

    private T? _value;

    public AttributePropertyValue(string name, T DefaultValue)
    {
        Name = name;
        this.DefaultValue = DefaultValue;
    }

    public static implicit operator T(AttributePropertyValue<T> attributePropertyValue) => attributePropertyValue.Value;

    public void ReadIfEmpty(KeyValuePair<string, TypedConstant> property)
    {
        if (Read) return;

        if (property.Key == Name && property.Value is T propertyValue)
        {
            _value = propertyValue;
            Read = true;
        }
    }
}

internal readonly record struct TextProviderAttributeData
{
    public readonly string Namespace;
    public readonly string ClassName;

    public readonly AttributePropertyValue<string> Filename = new(nameof(Filename), string.Empty);
    public readonly AttributePropertyValue<bool> IsDefault = new(nameof(IsDefault), false);

    public TextProviderAttributeData(string @namespace, string className, AttributeData attributeData)
    {
        Namespace = @namespace;
        ClassName = className;

        foreach (var property in attributeData.NamedArguments)
        {
            Filename.ReadIfEmpty(property);
            IsDefault.ReadIfEmpty(property);
        }
    }
}

internal readonly record struct TranslationTableAttributeData
{
    public readonly string Namespace;
    public readonly string ClassName;

    public readonly AttributePropertyValue<string> CurrentProviderAccessor = new(nameof(CurrentProviderAccessor), string.Empty);
    public readonly AttributePropertyValue<string> DefaultProviderAccessor = new(nameof(DefaultProviderAccessor), string.Empty);
    public readonly AttributePropertyValue<string> TableName = new(nameof(TableName), "Table");
    public readonly AttributePropertyValue<bool> GenerateDocs = new(nameof(GenerateDocs), true);
    public readonly AttributePropertyValue<bool> GenerateIdTable = new(nameof(GenerateIdTable), true);

    public TranslationTableAttributeData(string @namespace, string className, AttributeData attributeData)
    {
        Namespace = @namespace;
        ClassName = className;

        foreach (var property in attributeData.NamedArguments)
        {
            CurrentProviderAccessor.ReadIfEmpty(property);
            DefaultProviderAccessor.ReadIfEmpty(property);
            TableName.ReadIfEmpty(property);
            GenerateDocs.ReadIfEmpty(property);
            GenerateIdTable.ReadIfEmpty(property);
        }
    }

    public bool IsValid()
    {
        return SyntaxFacts.IsValidIdentifier(CurrentProviderAccessor.Value) &&
               SyntaxFacts.IsValidIdentifier(DefaultProviderAccessor.Value) &&
               SyntaxFacts.IsValidIdentifier(TableName.Value);
    }
}
