using System.Globalization;

namespace Jumbo.Services;

/// <summary>
/// Example phone number for the user's country, shown as the placeholder of the phone field ("+998 00 000 00 00" in Uzbekistan).
/// The country comes from the phone's time zone, then its region setting: no location permission is asked.
/// Same table as the website's <c>lp.phoneHint</c>.
/// </summary>
public static class PhoneHints
{
    private static readonly Dictionary<string, string> Hints = new()
    {
        ["UZ"] = "+998 00 000 00 00", ["RU"] = "+7 000 000 00 00", ["KZ"] = "+7 000 000 00 00", ["KG"] = "+996 000 00 00 00",
        ["TJ"] = "+992 00 000 00 00", ["TM"] = "+993 00 00 00 00", ["AZ"] = "+994 00 000 00 00", ["TR"] = "+90 000 000 00 00",
        ["UA"] = "+380 00 000 00 00", ["BY"] = "+375 00 000 00 00", ["AE"] = "+971 00 000 0000", ["KR"] = "+82 00 0000 0000",
        ["JP"] = "+81 00 0000 0000", ["CN"] = "+86 000 0000 0000", ["IN"] = "+91 00000 00000", ["GB"] = "+44 0000 000000",
        ["DE"] = "+49 000 00000000", ["US"] = "+1 000 000 0000", ["CA"] = "+1 000 000 0000"
    };

    private static readonly Dictionary<string, string> Zones = new()
    {
        ["Asia/Tashkent"] = "UZ", ["Asia/Samarkand"] = "UZ", ["Asia/Almaty"] = "KZ", ["Asia/Qostanay"] = "KZ", ["Asia/Aqtobe"] = "KZ",
        ["Asia/Aqtau"] = "KZ", ["Asia/Atyrau"] = "KZ", ["Asia/Oral"] = "KZ", ["Asia/Qyzylorda"] = "KZ", ["Asia/Bishkek"] = "KG",
        ["Asia/Dushanbe"] = "TJ", ["Asia/Ashgabat"] = "TM", ["Asia/Baku"] = "AZ", ["Europe/Istanbul"] = "TR", ["Europe/Kiev"] = "UA",
        ["Europe/Kyiv"] = "UA", ["Europe/Minsk"] = "BY", ["Asia/Dubai"] = "AE", ["Asia/Seoul"] = "KR", ["Asia/Tokyo"] = "JP",
        ["Asia/Shanghai"] = "CN", ["Asia/Kolkata"] = "IN", ["Asia/Calcutta"] = "IN", ["Europe/London"] = "GB", ["Europe/Berlin"] = "DE",
        ["Europe/Moscow"] = "RU", ["Europe/Kaliningrad"] = "RU", ["Europe/Samara"] = "RU", ["Asia/Yekaterinburg"] = "RU",
        ["Asia/Omsk"] = "RU", ["Asia/Novosibirsk"] = "RU", ["Asia/Krasnoyarsk"] = "RU", ["Asia/Irkutsk"] = "RU",
        ["Asia/Yakutsk"] = "RU", ["Asia/Vladivostok"] = "RU", ["Asia/Magadan"] = "RU", ["Asia/Kamchatka"] = "RU"
    };

    /// <summary>The phone's country (two letters) from its time zone, then its region setting; null when unknown.</summary>
    public static string? Country
    {
        get
        {
            string? country = null;
            try
            {
                var tz = TimeZoneInfo.Local.Id;
                if (!Zones.TryGetValue(tz, out country) && tz.StartsWith("America/", StringComparison.Ordinal)) country = "US";
            }
            catch { /* unknown time zone */ }

            if (country is null)
            {
                try { country = RegionInfo.CurrentRegion.TwoLetterISORegionName.ToUpperInvariant(); }
                catch { /* no region */ }
            }
            return country;
        }
    }

    /// <summary>The example number; Uzbekistan's when the country is unknown (the site is Uzbek).</summary>
    public static string Current => Country is { } country && Hints.TryGetValue(country, out var hint) ? hint : Hints["UZ"];
}
