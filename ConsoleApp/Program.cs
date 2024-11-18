using ConsoleApp;

var localization = new Localization();

Console.WriteLine(localization.Table.greeting);

localization.SetLanguage(SupportedLanguage.Polish);
Console.WriteLine(localization.Table.greeting);





