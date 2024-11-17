namespace Generator;

public readonly record struct LocalizedText
{
    public readonly string Text;
    public readonly int LineNumber;
    public readonly bool Untranslatable;

    public LocalizedText(string text, int lineNumber, bool untranslatable = false)
    {
        Text = text;
        LineNumber = lineNumber;
        Untranslatable = untranslatable;
    }
}
