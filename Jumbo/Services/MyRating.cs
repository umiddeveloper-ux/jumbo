using Jumbo.ApiClient;
using Jumbo.Shared.Entities;

namespace Jumbo.Services;

/// <summary>The learner's place in the weekly rating (scope: team | school | region | all).</summary>
public sealed record RatingSummary(string Scope, int Rank, int Total, int Words)
{
    public string ScopeKey => Scope switch
    {
        "team" => "Stats_ScopeTeam",
        "school" => "Stats_ScopeSchool",
        "region" => "Stats_ScopeRegion",
        _ => "Stats_ScopeAll"
    };
}

/// <summary>
/// Works out where the learner stands. The server counts the points (distinct words of the last 7 days);
/// the app picks the most personal scope the learner filled in (team, then school, then region) and falls back
/// to the whole country when they are not ranked there yet - the same order as the website's rating page.
/// </summary>
public static class MyRating
{
    public static async Task<RatingSummary?> LoadAsync(LpApiClient api, User? me)
    {
        var scope = !string.IsNullOrWhiteSpace(me?.Team) ? "team"
                  : !string.IsNullOrWhiteSpace(me?.School) ? "school"
                  : !string.IsNullOrWhiteSpace(me?.Region) ? "region" : "all";

        var data = await api.GetRatingAsync(scope, "week", 1);
        if (scope != "all" && (data is null || data.Missing || data.Me is null))
        {
            scope = "all";
            data = await api.GetRatingAsync(scope, "week", 1);
        }
        return data?.Me is { } m ? new RatingSummary(scope, m.Rank, data.Total, m.Words) : null;
    }
}
