namespace Jumbo.Shared;

/// <summary>
/// The three versions of the app. Bronze is free; Silver is paid and takes the limits away (no lives are lost and no LP are spent on
/// mistakes); Gold is Silver plus the adviser. A paid version is monthly: when the date it is paid until passes, the user is Bronze again.
/// </summary>
public static class Plans
{
    public const string Bronze = "bronze";
    public const string Silver = "silver";
    public const string Gold = "gold";

    public static readonly IReadOnlyList<string> All = [Bronze, Silver, Gold];

    /// <summary>The periods a paid version can be bought for.</summary>
    public const string Month = "month";
    public const string Year = "year";
    public static readonly IReadOnlyList<string> Periods = [Month, Year];

    /// <summary>The end of a period that starts at <paramref name="from"/> (a month, or a year).</summary>
    public static DateTime AddPeriod(DateTime from, string period) => period == Year ? from.AddYears(1) : from.AddMonths(1);

    public static string Normalize(string? plan) => plan?.Trim().ToLowerInvariant() switch { Silver => Silver, Gold => Gold, _ => Bronze };

    public static int Rank(string? plan) => Normalize(plan) switch { Silver => 1, Gold => 2, _ => 0 };

    /// <summary>The version that is really in force now: a paid one only until the paid-until date (a paid version without a date is not in force).</summary>
    public static string Effective(string? plan, DateTime? paidUntil, DateTime nowUtc) =>
        Rank(plan) > 0 && paidUntil is { } until && until > nowUtc ? Normalize(plan) : Bronze;

    /// <summary>No lives and no LP are lost on mistakes.</summary>
    public static bool NoLimits(string? plan) => Rank(plan) >= 1;

    /// <summary>The adviser (not built yet; the version is sold with the promise).</summary>
    public static bool Advisor(string? plan) => Rank(plan) >= 2;
}
