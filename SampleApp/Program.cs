using SampleApp;

var localization = new Localization();

var farewellId = R.farewell;
Console.WriteLine(localization.R[farewellId]);

var message = localization.StringResource(R.templated, true);
Console.WriteLine(message);

Console.WriteLine(localization.R.greetings);

localization.SetLanguage(SupportedLanguage.Polish);
Console.WriteLine(localization.R.greetings);

localization.SetLanguage(SupportedLanguage.German);
Console.WriteLine(localization.R.evening);
Console.WriteLine(localization.R.untranslated_key);
