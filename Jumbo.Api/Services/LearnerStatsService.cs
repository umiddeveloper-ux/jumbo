using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Numbers about one learner for the bot's /me and /top (the apps compute the same on their side).
public class LearnerStatsService(AppDbContext db, RatingService ratings)
{
    // CEFR level = level of the book of the first topic that is not fully won (the last book when everything is won).
    public async Task<string> GetLevelAsync(int userId)
    {
        var subjects = await db.Subjects.AsNoTracking()
            .OrderBy(s => s.BookId).ThenBy(s => s.SequenceNumber)
            .Select(s => new { s.SubjectId, s.BookId })
            .ToListAsync();
        if (subjects.Count == 0) return LanguageLevels.ForBook(1);

        var won = (await db.UserSubjectProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && p.FindMeWon && p.ListenWon && (p.PhraseWon || (p.SequenceWon && p.StepsWon)))
            .Select(p => p.SubjectId)
            .ToListAsync()).ToHashSet();

        var current = subjects.FirstOrDefault(s => !won.Contains(s.SubjectId));
        return LanguageLevels.ForBook(current?.BookId ?? subjects[^1].BookId);
    }

    // The most personal list the learner is in (team, school, region), the whole country when they have no place there yet.
    public async Task<(string Scope, RatingDto Data)> GetPlaceAsync(User me, int take)
    {
        var scope = !string.IsNullOrWhiteSpace(me.Team) ? "team"
                  : !string.IsNullOrWhiteSpace(me.School) ? "school"
                  : !string.IsNullOrWhiteSpace(me.Region) ? "region" : "all";

        var data = await ratings.GetAsync(me, scope, "week", take);
        if (scope != "all" && (data.Missing || data.Me is null))
        {
            scope = "all";
            data = await ratings.GetAsync(me, scope, "week", take);
        }
        return (scope, data);
    }
}
