using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// What a pupil has done in each book: topics (games won), words practised, exams. Feeds the teacher's team table
// ("current book") and the pupil's page with the full picture.
public static class StudentProgress
{
    private const int EnglishLanguageId = 1;   // the book's word list is counted once, by its English rows

    // The highest book in which the pupil has won a game or practised a word. Books are taken in order, so that is "where they are now".
    public static async Task<Dictionary<int, int>> CurrentBooksAsync(AppDbContext db, IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0) return new();

        var byGames = await (from p in db.UserSubjectProgresses.AsNoTracking()
                             join s in db.Subjects.AsNoTracking() on p.SubjectId equals s.SubjectId
                             where userIds.Contains(p.UserId) && (p.FindMeWon || p.SequenceWon || p.ListenWon || p.StepsWon || p.PhraseWon)
                             group s by p.UserId into g
                             select new { UserId = g.Key, Book = g.Max(s => s.BookId) }).ToListAsync();
        var byWords = await (from uw in db.UsersWords.AsNoTracking()
                             join w in db.Words.AsNoTracking() on uw.WordId equals w.WordId
                             join s in db.Subjects.AsNoTracking() on w.SubjectId equals s.SubjectId
                             where userIds.Contains(uw.UserId)
                             group s by uw.UserId into g
                             select new { UserId = g.Key, Book = g.Max(s => s.BookId) }).ToListAsync();

        var result = new Dictionary<int, int>();
        foreach (var x in byGames.Concat(byWords))
            result[x.UserId] = result.TryGetValue(x.UserId, out var b) ? Math.Max(b, x.Book) : x.Book;
        return result;
    }

    public static async Task<StudentProgressDto> BuildAsync(AppDbContext db, User user, string className)
    {
        var userId = user.UserId;

        var subjects = await db.Subjects.AsNoTracking()
            .OrderBy(s => s.BookId).ThenBy(s => s.SequenceNumber)
            .Select(s => new { s.SubjectId, s.BookId, s.SubjectName, s.SequenceNumber })
            .ToListAsync();

        var wordTotals = await db.Words.AsNoTracking()
            .Where(w => w.LanguageId == EnglishLanguageId)
            .GroupBy(w => w.SubjectId)
            .Select(g => new { SubjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubjectId, x => x.Count);

        // Distinct practised words per topic.
        var learnedRows = await (from uw in db.UsersWords.AsNoTracking()
                                 join w in db.Words.AsNoTracking() on uw.WordId equals w.WordId
                                 where uw.UserId == userId
                                 select new { w.SubjectId, w.WordId }).Distinct().ToListAsync();
        var learned = learnedRows.GroupBy(x => x.SubjectId).ToDictionary(g => g.Key, g => g.Count());

        var progress = await db.UserSubjectProgresses.AsNoTracking()
            .Where(p => p.UserId == userId).ToDictionaryAsync(p => p.SubjectId);

        var exams = (await db.ExamResults.AsNoTracking()
            .Where(e => e.UserId == userId && e.TotalCount > 0)
            .Select(e => new { e.BookNumber, e.CorrectCount, e.TotalCount, e.TakenAt })
            .ToListAsync()).ToLookup(e => e.BookNumber);

        var lastActive = await db.UserDailyProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && p.WordsCount > 0)
            .Select(p => (DateOnly?)p.Date).MaxAsync();

        var books = new List<StudentBookDto>();
        foreach (var group in subjects.GroupBy(s => s.BookId))
        {
            var topics = new List<StudentTopicDto>();
            foreach (var s in group)
            {
                progress.TryGetValue(s.SubjectId, out var p);
                var total = wordTotals.GetValueOrDefault(s.SubjectId);
                topics.Add(new StudentTopicDto
                {
                    SubjectId = s.SubjectId,
                    Name = s.SubjectName,
                    SequenceNumber = s.SequenceNumber,
                    FindMe = p?.FindMeWon ?? false,
                    Sequence = p?.SequenceWon ?? false,
                    Listen = p?.ListenWon ?? false,
                    Steps = p?.StepsWon ?? false,
                    Phrase = p?.PhraseWon ?? false,
                    Done = p?.AllWon ?? false,
                    WordsTotal = total,
                    WordsLearned = Math.Min(total, learned.GetValueOrDefault(s.SubjectId))
                });
            }

            var ex = exams[group.Key].OrderBy(e => e.TakenAt).ToList();
            books.Add(new StudentBookDto
            {
                BookNumber = group.Key,
                TopicsTotal = topics.Count,
                TopicsDone = topics.Count(t => t.Done),
                TopicsStarted = topics.Count(t => !t.Done && (t.FindMe || t.Sequence || t.Listen || t.Steps || t.Phrase || t.WordsLearned > 0)),
                WordsTotal = topics.Sum(t => t.WordsTotal),
                WordsLearned = topics.Sum(t => t.WordsLearned),
                ExamCount = ex.Count,
                ExamBestPercent = ex.Count > 0 ? ex.Max(e => (int)Math.Round(e.CorrectCount * 100.0 / e.TotalCount)) : null,
                ExamLastPercent = ex.Count > 0 ? (int)Math.Round(ex[^1].CorrectCount * 100.0 / ex[^1].TotalCount) : null,
                ExamLastAt = ex.Count > 0 ? ex[^1].TakenAt : null,
                Topics = topics
            });
        }

        int? current = books.Where(b => b.TopicsDone > 0 || b.TopicsStarted > 0).Select(b => (int?)b.BookNumber).DefaultIfEmpty(null).Max();
        foreach (var b in books) b.IsCurrent = b.BookNumber == current;

        return new StudentProgressDto
        {
            UserId = userId,
            Name = $"{user.FirstName} {user.LastName}".Trim(),
            Username = user.Username,
            ClassName = className,
            LpPoints = user.LpPoints,
            Streak = user.Streak,
            LastActiveDate = lastActive,
            CurrentBook = current,
            Books = books
        };
    }
}
