using Jumbo.Resources.Strings;
using System.Globalization;

namespace Jumbo.Services;

public static class LocalizationService
{
    private const string LanguageKey = "selected_language";

    /// <summary>Language codes of the app UI, in the order of the Language table (1=en, 2=ru, 3=uz).</summary>
    public static readonly (string Code, string Name)[] Languages = [("ru", "Русский"), ("en", "English"), ("uz", "O'zbekcha")];

    public static string CurrentCode => Preferences.Default.Get(LanguageKey, "ru");

    public static void SetLanguage(string cultureCode)
    {
        var culture = new CultureInfo(cultureCode);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        AppResources.Culture = culture;

        // Сохраняем выбор пользователя навсегда
        Preferences.Default.Set(LanguageKey, cultureCode);

        // Every {u:Tr ...} text in the UI re-reads its string.
        LocalizationManager.Instance.Refresh();
    }

    public static void RestoreLanguage()
    {
        // The saved choice; on the first start the language is detected (see DetectDefault).
        string savedLanguage = Preferences.Default.Get(LanguageKey, DetectDefault());

        SetLanguage(savedLanguage);
    }

    /// <summary>
    /// The language for a phone that has not chosen one (same rule as the website): Uzbekistan -> uz; Russia and the Russian-speaking
    /// countries -> ru; otherwise the phone's own language if it is one of ours, and English by default.
    /// </summary>
    public static string DetectDefault()
    {
        var country = PhoneHints.Country;
        if (country == "UZ") return "uz";
        if (country is "RU" or "BY" or "KZ" or "KG" or "TJ" or "TM" or "UA" or "AM" or "AZ" or "MD") return "ru";

        return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "uz" => "uz",
            "ru" => "ru",
            _ => "en"
        };
    }
}
