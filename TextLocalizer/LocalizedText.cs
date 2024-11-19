namespace TextLocalizer;

public readonly record struct LocalizedText
{
    public readonly string Text;
    public readonly int LineNumber;
    public readonly bool IsUntranslatable;

    public LocalizedText(string text, int lineNumber, bool isUntranslatable = false)
    {
        Text = text;
        LineNumber = lineNumber;
        IsUntranslatable = isUntranslatable;
    }
}

public readonly record struct IndexedLocalizedText
{
    public readonly string Text;
    public readonly int Index;
    public readonly bool IsUntranslatable;

    public IndexedLocalizedText(string text, int index, bool isUntranslatable = false)
    {
        Text = text;
        Index = index;
        IsUntranslatable = isUntranslatable;
    }
}

