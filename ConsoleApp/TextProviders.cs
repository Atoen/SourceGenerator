using Generator.Generated;

namespace ConsoleApp;

[TranslationProvider(Filename = "english.yml", IsDefault = true)]
public partial class EnglishTextProvider;

[TranslationProvider(Filename = "polish.yml")]
public partial class PolishTextProvider;

