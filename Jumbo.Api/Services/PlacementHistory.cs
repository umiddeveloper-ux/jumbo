using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// The stored attempts of the level test (CEFR): the latest of a learner, for the learner and for his teacher.
public static class PlacementHistory
{
    public static double?[] Shares(PlacementAttempt r) => [r.ShareA1, r.ShareA2, r.ShareB1, r.ShareB2];

    public static PlacementSavedDto Saved(PlacementAttempt r) => new()
    {
        Stage = r.Stage, Level = r.Level, RecommendedBook = r.RecommendedBook,
        TakenAt = DateTime.SpecifyKind(r.TakenAt, DateTimeKind.Utc), Shares = Shares(r)
    };

    public static PlacementBadgeDto Badge(PlacementAttempt r) => new()
    {
        Stage = r.Stage, Level = r.Level, TakenAt = DateTime.SpecifyKind(r.TakenAt, DateTimeKind.Utc), Shares = Shares(r)
    };

    // The latest attempt of each of the learners (a learner without any is not in the result).
    public static async Task<Dictionary<int, PlacementAttempt>> LatestAsync(AppDbContext db, IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0) return new();
        var all = await db.PlacementAttempts.AsNoTracking()
            .Where(r => userIds.Contains(r.UserId))
            .OrderByDescending(r => r.TakenAt).ThenByDescending(r => r.PlacementAttemptId)
            .ToListAsync();
        return all.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => g.First());
    }
}
