namespace Generator;

public readonly struct InvalidIdentifierError
{
    public readonly string IdentifierFor;
    public readonly string Value;

    public InvalidIdentifierError(string identifierFor, string value)
    {
        IdentifierFor = identifierFor;
        Value = value;
    }
}

public readonly struct MissingAttributeError
{
    public readonly string AttributeName;

    public MissingAttributeError(string attributeName)
    {
        AttributeName = attributeName;
    }
}