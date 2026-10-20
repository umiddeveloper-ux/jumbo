namespace Jumbo.Shared;

// The regions of Uzbekistan (+ "other") stored on users, schools and requests as stable codes.
public static class RegionCodes
{
    public static readonly string[] All =
    [
        "tashkent-city", "tashkent-region", "samarkand", "bukhara", "fergana", "andijan", "namangan",
        "kashkadarya", "surkhandarya", "khorezm", "navoi", "jizzakh", "sirdarya", "karakalpakstan", "other"
    ];

    public static bool IsValid(string? code) => code is not null && Array.IndexOf(All, code) >= 0;
}
