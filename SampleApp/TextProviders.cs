using TextLocalizer;

namespace SampleApp;

[TranslationProvider(Filename = "english.yml", IsDefault = true)]
public partial class EnglishTextProvider;

[TranslationProvider(Filename = "polish.yml")]
public partial class PolishTextProvider;

[TranslationProvider(Filename = "german.yml")]
public partial class GermanTextProvider;
