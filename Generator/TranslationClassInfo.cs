namespace Generator;

public readonly record struct TranslationClassInfo
{
    public readonly string ClassName;
    public readonly string Filename;
    public readonly string Directory;

    public TranslationClassInfo(string className, string filename, string directory)
    {
        ClassName = className;
        Filename = filename;
        Directory = directory;
    }
}
