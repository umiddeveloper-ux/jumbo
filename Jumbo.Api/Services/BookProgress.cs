using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Which books a learner may open and which one they are in (shared by api/progress/books/unlocked, current-book and the welcome summary).
public static class BookProgress
{
    // The books the learner may open: book 1; every book up to the one they already play in (nobody who started before the
    // check existed is locked out); every book opened by passing the 3-step check over the previous one.
    public static async Task<List<int>> UnlockedAsync(AppDbContext db, int userId)
    {
        var open = (await db.UserBookUnlocks.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.BookNumber).ToListAsync()).ToHashSet();
        var played = await StudentProgress.CurrentBooksAsync(db, [userId]);
        var upTo = played.TryGetValue(userId, out var current) ? current : 1;
        for (var b = 1; b <= Math.Max(1, upTo); b++) open.Add(b);
        return open.OrderBy(b => b).ToList();
    }

    // The book the learner is in: the first open book that still has a topic with a game not won (the last open one when all are won).
    public static async Task<int> CurrentBookAsync(AppDbContext db, int userId) => (await CurrentBooksAsync(db, [userId]))[userId];

    // The same for many learners at once (the leaderboard): a fixed number of queries, whatever the number of learners. One rule for both.
    public static async Task<Dictionary<int, int>> CurrentBooksAsync(AppDbContext db, IReadOnlyCollection<int> userIds)
    {
        var result = new Dictionary<int, int>();
        if (userIds.Count == 0) return result;

        var unlocks = (await db.UserBookUnlocks.AsNoTracking().Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.BookNumber }).ToListAsync()).ToLookup(u => u.UserId, u => u.BookNumber);
        var played = await StudentProgress.CurrentBooksAsync(db, userIds);
        var topics = await db.Subjects.AsNoTracking().OrderBy(s => s.BookId).ThenBy(s => s.SequenceNumber)
            .Select(s => new { s.SubjectId, s.BookId }).ToListAsync();
        var wonRows = (await db.UserSubjectProgresses.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId) && p.FindMeWon && p.ListenWon && (p.PhraseWon || (p.SequenceWon && p.StepsWon)))
            .Select(p => new { p.UserId, p.SubjectId }).ToListAsync()).ToLookup(p => p.UserId, p => p.SubjectId);

        foreach (var userId in userIds.Distinct())
        {
            var open = unlocks[userId].ToHashSet();
            var upTo = played.TryGetValue(userId, out var current) ? current : 1;
            for (var b = 1; b <= Math.Max(1, upTo); b++) open.Add(b);

            var won = wonRows[userId].ToHashSet();
            var next = topics.FirstOrDefault(t => open.Contains(t.BookId) && !won.Contains(t.SubjectId));
            result[userId] = next?.BookId ?? open.Max();
        }
        return result;
    }
}
