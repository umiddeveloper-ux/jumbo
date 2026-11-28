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
    public static async Task<int> CurrentBookAsync(AppDbContext db, int userId)
    {
        var open = await UnlockedAsync(db, userId);

        var topics = await db.Subjects.AsNoTracking()
            .Where(s => open.Contains(s.BookId))
            .OrderBy(s => s.BookId).ThenBy(s => s.SequenceNumber)
            .Select(s => new { s.SubjectId, s.BookId })
            .ToListAsync();
        var won = (await db.UserSubjectProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && p.FindMeWon && p.ListenWon && (p.PhraseWon || (p.SequenceWon && p.StepsWon)))
            .Select(p => p.SubjectId).ToListAsync()).ToHashSet();

        var next = topics.FirstOrDefault(t => !won.Contains(t.SubjectId));
        return next?.BookId ?? open.Max();
    }
}
