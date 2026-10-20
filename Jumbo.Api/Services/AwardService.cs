using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

/// <summary>
/// Gives a learner the awards they have earned. The awards are the rows of the seed file in its order (the same in every language,
/// the rows of one award share the picture): 0 = "You started learning" (given as soon as the learner is registered), 1..14 = "Book N: Completed", 15..19 = the levels A1, A2, B1, B2, C1.
/// An award is stored once per learner, on the English row; the other languages find it by the picture.
/// It is checked whenever the client asks for the new awards, so no game has to know about awards.
/// </summary>
public class AwardService(AppDbContext db)
{
    private const int EnglishLanguageId = 1;
    private const int BookAwards = 14;                     // "Book 1..14: Completed" (the edition has 10 books, the rest come later)

    // The LP an award pays: 0 = "You started learning", 1..14 = a book, 15..19 = a level.
    public static int BonusOf(int index) => index == 0 ? 20 : index <= BookAwards ? 100 : 300;

    // A level is mastered when all the books of it are completed (see LanguageLevels).
    private static readonly (int FirstBook, int LastBook)[] LevelBooks = [(1, 3), (4, 6), (7, 10), (11, 14), (15, 15)];

    /// <summary>Awards the learner has earned and does not have yet are written down.</summary>
    public async Task EvaluateAsync(int userId)
    {
        // the awards in the order of the seed file
        var awards = await db.Awards.AsNoTracking().Where(a => a.LanguageId == EnglishLanguageId).OrderBy(a => a.AwardId).ToListAsync();
        if (awards.Count == 0) return;

        var has = (await db.UserAwards.AsNoTracking().Where(ua => ua.UserId == userId).Select(ua => ua.AwardsId).ToListAsync()).ToHashSet();
        if (has.Count >= awards.Count) return;

        var progress = await db.UserSubjectProgresses.AsNoTracking().Where(p => p.UserId == userId).ToListAsync();
        var done = progress.Where(p => p.PartsDone >= UserSubjectProgress.Parts).Select(p => p.SubjectId).ToHashSet();

        var subjects = await db.Subjects.AsNoTracking().Select(s => new { s.SubjectId, s.BookId }).ToListAsync();
        var bookDone = new HashSet<int>();
        foreach (var g in subjects.GroupBy(s => s.BookId))
            if (g.Any() && g.All(s => done.Contains(s.SubjectId))) bookDone.Add(g.Key);

        var earned = new List<int>();
        earned.Add(0);                                                                           // "You started learning": given at once, on registration
        for (int b = 1; b <= BookAwards; b++)
            if (bookDone.Contains(b)) earned.Add(b);
        for (int l = 0; l < LevelBooks.Length; l++)
        {
            var (first, last) = LevelBooks[l];
            if (Enumerable.Range(first, last - first + 1).All(bookDone.Contains)) earned.Add(1 + BookAwards + l);
        }

        var now = DateTime.UtcNow;
        int bonus = 0;
        foreach (var index in earned.Distinct())
        {
            if (index >= awards.Count || has.Contains(awards[index].AwardId)) continue;
            db.UserAwards.Add(new UserAwards { UserId = userId, AwardsId = awards[index].AwardId, AwardedAt = now });
            bonus += BonusOf(index);
        }
        if (bonus > 0)
        {
            // an award pays LP (the clients read them again when they show the pop-up)
            var user = await db.Users.SingleAsync(u => u.UserId == userId);
            user.LpPoints += bonus;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>The awards the learner has earned and has not been told about yet (each is returned once), in the language asked for.</summary>
    public async Task<List<AwardItem>> TakeNewAsync(int userId, int languageId)
    {
        await EvaluateAsync(userId);
        var awards = await db.Awards.AsNoTracking().Where(a => a.LanguageId == EnglishLanguageId).OrderBy(a => a.AwardId).ToListAsync();

        var fresh = await db.UserAwards.Where(ua => ua.UserId == userId && ua.NotifiedAt == null).ToListAsync();
        if (fresh.Count == 0) return [];

        var bySvg = await (from ua in db.UserAwards.AsNoTracking()
                           where ua.UserId == userId && ua.NotifiedAt == null
                           join a in db.Awards.AsNoTracking() on ua.AwardsId equals a.AwardId
                           select a.SvgImageId).ToListAsync();
        var shown = await db.Awards.AsNoTracking().Where(a => a.LanguageId == languageId && bySvg.Contains(a.SvgImageId)).OrderBy(a => a.AwardId).ToListAsync();
        var svgLookup = await db.SvgImages.AsNoTracking().Where(s => bySvg.Contains(s.SvgImageId)).ToDictionaryAsync(s => s.SvgImageId, s => s.SVG);

        // what each of them paid
        var order = awards.Select((a, i) => (a.AwardId, a.SvgImageId, Index: i)).ToList();
        var bonusBySvg = new Dictionary<int, int>();
        foreach (var ua in fresh)
            if (order.FirstOrDefault(o => o.AwardId == ua.AwardsId) is { } o && o.AwardId != 0) bonusBySvg[o.SvgImageId] = BonusOf(o.Index);

        var now = DateTime.UtcNow;
        foreach (var ua in fresh) ua.NotifiedAt = now;
        await db.SaveChangesAsync();

        return shown.Select(a => new AwardItem
        {
            Title = a.AwardName,
            SvgImage = svgLookup.GetValueOrDefault(a.SvgImageId, string.Empty),
            IsAchieved = true,
            LpBonus = bonusBySvg.GetValueOrDefault(a.SvgImageId)
        }).ToList();
    }
}
