using System.Globalization;
using Microsoft.Extensions.Localization;

namespace JumboWeb.Services;

/// <summary>
/// Per-user localizer. Reads strings from AppResources.*.resx
/// based on the current user's AppLangId (1=en, 2=ru, 3=uz).
/// Register as Scoped so it shares the same UserPreferencesService instance.
/// </summary>
public class AppLocalizer
{
    private readonly IStringLocalizer<AppResources> _loc;
    private readonly UserPreferencesService _prefs;

    public AppLocalizer(IStringLocalizer<AppResources> loc, UserPreferencesService prefs)
    {
        _loc = loc;
        _prefs = prefs;
    }

    public string this[string key]
    {
        get
        {
            var langCode = _prefs.AppLangId switch { 2 => "ru", 3 => "uz", 4 => "tr", _ => "en" };
            var saved = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(langCode);
                var result = _loc[key];
                return result.ResourceNotFound ? key : result.Value;
            }
            finally
            {
                CultureInfo.CurrentUICulture = saved;
            }
        }
    }

    public string Format(string key, params object[] args) => string.Format(this[key], args);

    // The name of a topic in the user's language (the books keep one English name; Turkish has its own).
    public string Topic(Jumbo.Shared.Entities.Subject s) => Jumbo.Shared.TopicNames.Localize(_prefs.AppLangId, s.BookId, s.SequenceNumber, s.SubjectName);
}
