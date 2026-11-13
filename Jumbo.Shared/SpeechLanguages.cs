namespace Jumbo.Shared;

/// <summary>
/// The voice that says the words of a study language (the language ids follow the API's seed order: 1 = English, 2 = Russian,
/// 3 = Uzbek, 4 = Turkish). Browsers take the BCP-47 code ("tr-TR"), the phone's speech engine takes the prefix ("tr").
/// </summary>
public static class SpeechLanguages
{
    public static string Code(int languageId) => languageId switch { 2 => "ru-RU", 3 => "uz-UZ", 4 => "tr-TR", _ => "en-US" };

    public static string Prefix(int languageId) => languageId switch { 2 => "ru", 3 => "uz", 4 => "tr", _ => "en" };
}
