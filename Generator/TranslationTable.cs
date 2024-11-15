namespace Generator;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public sealed class TranslationTable : Attribute
{
    public string Filename { get; }

    public TranslationTable(string filename)
    {
        Filename = filename;
    }

}