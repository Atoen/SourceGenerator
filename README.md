# TextLocalizer
A source-generated text translation provider using YAML files.

## Setup

### 1. Create Translation Files

First, create the YAML (`.yaml` or `.yml`) files containing the translations.
The keys must be valid C# identifiers.

```yaml
# Blank and comment lines are ignored.
# The ordering of keys does not need to match between files.
hello_world: Hello, World!
goodbye: Goodbye.

# Quoted values (useful for special characters or preserving whitespace)
quoted: "quoted: value!"
quoted2: 'some other value'
```

### 2. Include Localization Files in the Build

Ensure that the YAML files containing localization data are included in the build process.
Add the following configuration to your project file (`.csproj`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        ...
    </PropertyGroup>


    <ItemGroup>
        <PackageReference Include="TextLocalizer" Version="1.0.8" />
    </ItemGroup>

    <ItemGroup>
        <AdditionalFiles Include="path\to\english.yml" />
        <AdditionalFiles Include="path\to\other.yaml" />
    </ItemGroup>
</Project>
```

### 3. Create Provider Classes

Create provider classes for each language you want to support.
Exactly one of these classes should be marked as the default language.

```csharp
using TextLocalizer;

[TranslationProvider(Filename = "english.yml", IsDefault = true)]
public partial class EnglishTextProvider;

[TranslationProvider(Filename = "other.yaml")]
public partial class OtherTextProvider;
```

Finally, create the class providing the correct localized keys:

```csharp
using TextLocalizer;
using TextLocalizer.Types;

// You can represent the languages in any way you like.
public enum SupportedLanguage
{
    English,
    Other
}

// The accessors are nescessary for the localization to work
[LocalizationTable(
    CurrentProviderAccessor = nameof(Provider),
    DefaultProviderAccessor = nameof(DefaultProvider),
    TableName = "R" // Defaults to "Table"
)]
public partial class Localization
{
    // Caching the providers might be useful
    private readonly Dictionary<SupportedLanguage, ILocalizedTextProvider> _textProviders = new();
    
    public static SupportedLanguage DefaultLanguage { get; set; } = SupportedLanguage.English;
    public SupportedLanguage Language { get; set; } = DefaultLanguage;
    
    private ILocalizedTextProvider Provider => GetLanguageProvider(Language);
    private ILocalizedTextProvider DefaultProvider => GetLanguageProvider(DefaultLanguage);
    
    // You can implement custom logic to determine which provider to use
    private ILocalizedTextProvider GetLanguageProvider(SupportedLanguage language)
    {
        if (_textProviders.TryGetValue(language, out var provider))
        {
            return provider;
        }

        var newProvider = CreateProvider(language);
        _textProviders[language] = newProvider;

        return newProvider;
    }
    
    private static ILocalizedTextProvider CreateProvider(SupportedLanguage language) => language switch
    {
        SupportedLanguage.English => new EnglishTextProvider(),
        SupportedLanguage.Other => new OtherTextProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(language))
    };
}

```

## Usage

```csharp
var localization = new Localization();

Console.WriteLine(localization.R.greetings);

localization.SetLanguage(SupportedLanguage.Other);
Console.WriteLine(localization.R.greetings);
```

## Untranslatable and Missing Keys

The generator emits warnings about missing or extra keys in localized dictionaries.
If a key is not found in the localized file, the value from the default file is used instead.

```
0>polish.yml(1,1): Warning TL001 : The key 'missing_key' is missing its translation in polish.yml file
0>polish.yml(6,1): Warning TL002 : File polish.yml contains key 'extra_key', which is not present in the main translations file
```

You can mark the key in the main dictionary file as untranslatable with a comment:
```yaml
special_key: Special value # untranslatable
```

When such a key is found in a localized file, its value is ignored, and another warning is emitted:
```
0>polish.yml(7,1): Warning TL003 : File polish.yml contains key 'untranslated_key', which is marked as untranslatable
```


