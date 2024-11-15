namespace Generator;


public static class SourceGenerationHelper
{
    public const string TableAttribute =
        """
        #nullable enable
        
        namespace Generator.Generated
        {
            [System.AttributeUsage(System.AttributeTargets.Class)]
            public class TranslationTableAttribute : System.Attribute
            {
                public string Filename { get; }
                public string? Directory { get; }
                
                public TranslationTableAttribute(string filename)
                {
                    Filename = filename;
                }
                
                public TranslationTableAttribute(string filename, string directory)
                {
                    Filename = filename;
                    Directory = directory;
                }
            }
        }
        
        #nullable restore
        """;
}


