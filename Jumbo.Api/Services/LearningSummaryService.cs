using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// The numbers of the "welcome back" dialog. A few aggregates, no word rows are loaded. Definitions follow the home page:
//  - learned words = the words of the learner's study language that have a users_words row (a game was won with them, or they were practised);
//    the total is all the words of the course in that language (the same number as api/words/count);
//  - the book the learner is in = BookProgress.CurrentBookAsync (the same as api/progress/current-book);
//  - the percent of the book = the parts of its topics done (three games per topic), as the book card of the home page shows.
public class LearningSummaryService(AppDbContext db, DailyProgressService daily)
{
    // null for anybody but a learner (teachers, directors and the admin have no course of their own).
    public async Task<LearningSummaryDto?> BuildAsync(int userId)
    {
        var user = await db.Users.AsNoTracking().Where(u => u.UserId == userId)
            .Select(u => new { u.Role, u.StudyLanguageId }).SingleAsync();
        if (user.Role != UserRole.Learner) return null;

        var language = user.StudyLanguageId;
        var code = await db.Languages.AsNoTracking().Where(l => l.LanguageId == language).Select(l => l.ShorName).SingleOrDefaultAsync() ?? "en";

        var total = await db.Words.AsNoTracking().CountAsync(w => w.LanguageId == language);
        var learned = await (from uw in db.UsersWords.AsNoTracking()
                             join w in db.Words.AsNoTracking() on uw.WordId equals w.WordId
                             where uw.UserId == userId && w.LanguageId == language
                             select w.WordId).Distinct().CountAsync();

        var dto = new LearningSummaryDto
        {
            HasStarted = learned > 0,
            StudyLanguageId = language,
            StudyLanguageCode = code,
            LearnedWords = learned,
            TotalWords = total,
            CoursePercent = LearningPercent.Course(learned, total),
        };
        if (!dto.HasStarted) return dto;            // nothing to tell yet: the rest costs queries for nothing

        var book = await BookProgress.CurrentBookAsync(db, userId);
        var topicIds = await db.Subjects.AsNoTracking().Where(s => s.BookId == book).Select(s => s.SubjectId).ToListAsync();
        var rows = await db.UserSubjectProgresses.AsNoTracking()
            .Where(p => p.UserId == userId && topicIds.Contains(p.SubjectId)).ToListAsync();

        dto.CurrentBook = book;
        dto.Level = LanguageLevels.ForBook(book);
        dto.BookTopics = topicIds.Count;
        dto.BookTopicsPassed = rows.Count(p => p.AllWon);
        dto.BookPercent = LearningPercent.Book(rows.Sum(p => p.PartsDone), topicIds.Count * UserSubjectProgress.Parts);
        dto.Streak = (await daily.GetAsync(userId)).Streak;
        return dto;
    }
}
