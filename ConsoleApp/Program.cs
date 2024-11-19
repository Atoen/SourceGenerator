using ConsoleApp;

var localization = new Localization();

Console.WriteLine(localization.R.greetings);

localization.SetLanguage(SupportedLanguage.Polish);
Console.WriteLine(localization.R.greetings);

localization.SetLanguage(SupportedLanguage.German);
Console.WriteLine(localization.R.evening);
Console.WriteLine(localization.R.untranslated_key);
