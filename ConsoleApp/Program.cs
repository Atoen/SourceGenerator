using ConsoleApp;

var localization = new Localization();
localization.SetLanguage(SupportedLanguage.Polish);

var localizedText = localization.R.hello_world;

Console.WriteLine(localizedText);
Console.WriteLine(localizedText);



