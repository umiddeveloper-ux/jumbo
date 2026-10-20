namespace Jumbo.Services;

/// <summary>Region codes stored on the user (same list as the website). The display name is the string "Region_&lt;code&gt;".</summary>
public static class UzRegions
{
    public static readonly string[] Codes =
    [
        "tashkent-city", "tashkent-region", "samarkand", "bukhara", "fergana", "andijan", "namangan",
        "kashkadarya", "surkhandarya", "khorezm", "navoi", "jizzakh", "sirdarya", "karakalpakstan", "other"
    ];

    public static List<RegionItem> Items() => Codes.Select(c => new RegionItem(c, LocalizationManager.Get($"Region_{c}"))).ToList();
}

public record RegionItem(string Code, string Name);
