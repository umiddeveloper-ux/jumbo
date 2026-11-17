namespace JumboWeb.Services;

// Interface languages of the site: 1 = English, 2 = Russian, 3 = Uzbek, 4 = Turkish (same ids as the Languages table).
// Guests keep their choice in the "jumbo-lang" cookie; signed-in players in their profile (AppLanguageId).
// Before anybody has chosen, the language follows the visitor's country ("jumbo-geo" cookie, set by a small script in the page head
// from the browser's time zone: Uzbekistan -> Uzbek, Russia and the Russian-speaking countries -> Russian, Turkey -> Turkish), then
// the browser language; English when nothing matches.
public static class AppLanguages
{
    public const string CookieName = "jumbo-lang";
    public const string GeoCookieName = "jumbo-geo";
    public const string DefaultCode = "en";

    public static readonly (int Id, string Code, string Short, string Name)[] All =
    [
        (2, "ru", "RU", "Русский"),
        (1, "en", "EN", "English"),
        (3, "uz", "UZ", "O'zbekcha"),
        (4, "tr", "TR", "Türkçe"),
    ];

    public static string ToCode(int id) => id switch { 1 => "en", 3 => "uz", 4 => "tr", _ => "ru" };

    public static int ToId(string? code) => code?.ToLowerInvariant() switch { "ru" => 2, "uz" => 3, "tr" => 4, _ => 1 };

    // The visitor's own choice first; then the country-based guess; then the browser's Accept-Language (uz / ru / tr / en); otherwise English.
    public static string Resolve(HttpContext? context)
    {
        if (context is null) return DefaultCode;

        var cookie = context.Request.Cookies[CookieName];
        if (cookie is "ru" or "en" or "uz" or "tr") return cookie;

        var geo = context.Request.Cookies[GeoCookieName];
        if (geo is "ru" or "en" or "uz" or "tr") return geo;

        var header = context.Request.Headers.AcceptLanguage.ToString();
        foreach (var part in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = part.Split(';')[0].ToLowerInvariant();
            if (tag.StartsWith("uz")) return "uz";
            if (tag.StartsWith("ru")) return "ru";
            if (tag.StartsWith("tr")) return "tr";
            if (tag.StartsWith("en")) return "en";
        }
        return DefaultCode;
    }
}
