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
            var langCode = _prefs.AppLangId switch { 2 => "ru", 3 => "uz", _ => "en" };
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
}
