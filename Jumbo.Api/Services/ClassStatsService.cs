using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Activity and exam numbers for a team. The same figures will feed the director's report, so they live here
// and not in a controller. Only learners the teacher has approved count; pending ones are listed separately.
// Days are app-time days (see DailyProgressService.Today).
public class ClassStatsService(AppDbContext db)
{
    private const int ExamWindowDays = 30;

    public async Task<List<ClassDto>> GetClassesAsync(IReadOnlyCollection<SchoolClass> classes)
    {
        if (classes.Count == 0) return new();

        var classIds = classes.Select(c => c.ClassId).ToList();
        var teacherIds = classes.Select(c => c.TeacherId).Distinct().ToList();
        var teachers = await db.Users.AsNoTracking()
            .Where(u => teacherIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.UserId);

        var members = await db.Users.AsNoTracking()
            .Where(u => u.ClassId != null && classIds.Contains(u.ClassId.Value) && u.Role == UserRole.Learner && u.ClassApproved)
            .Select(u => new { u.UserId, ClassId = u.ClassId!.Value })
            .ToListAsync();
        var pending = (await db.Users.AsNoTracking()
            .Where(u => u.ClassId != null && classIds.Contains(u.ClassId.Value) && u.Role == UserRole.Learner && !u.ClassApproved)
            .GroupBy(u => u.ClassId!.Value)
            .Select(g => new { ClassId = g.Key, Count = g.Count() })
            .ToListAsync()).ToDictionary(x => x.ClassId, x => x.Count);
        var memberIds = members.Select(m => m.UserId).ToList();

        var weekFrom = DailyProgressService.Today.AddDays(-6);
        var activeUsers = (await db.UserDailyProgresses.AsNoTracking()
            .Where(p => memberIds.Contains(p.UserId) && p.Date >= weekFrom && p.WordsCount > 0)
            .Select(p => p.UserId)
            .Distinct()
            .ToListAsync()).ToHashSet();

        var since = DateTime.UtcNow.AddDays(-ExamWindowDays);
        var exams = await db.ExamResults.AsNoTracking()
            .Where(e => memberIds.Contains(e.UserId) && e.TakenAt >= since)
            .Select(e => new { e.UserId, e.TotalCount, e.CorrectCount })
            .ToListAsync();
        var examsByUser = exams.ToLookup(e => e.UserId);

        return classes.Select(c =>
        {
            var ids = members.Where(m => m.ClassId == c.ClassId).Select(m => m.UserId).ToList();
            var percents = ids.SelectMany(id => examsByUser[id]).Where(e => e.TotalCount > 0)
                .Select(e => e.CorrectCount * 100.0 / e.TotalCount).ToList();
            teachers.TryGetValue(c.TeacherId, out var t);
            return new ClassDto
            {
                ClassId = c.ClassId,
                Name = c.Name,
                IsArchived = c.IsArchived,
                TeacherName = t is null ? null : $"{t.FirstName} {t.LastName}".Trim(),
                StudentCount = ids.Count,
                PendingCount = pending.GetValueOrDefault(c.ClassId),
                ActiveWeek = ids.Count(activeUsers.Contains),
                AvgExamPercent = percents.Count > 0 ? (int)Math.Round(percents.Average()) : null
            };
        }).ToList();
    }

    public async Task<List<ClassStudentDto>> GetStudentsAsync(int classId)
    {
        var users = await db.Users.AsNoTracking()
            .Where(u => u.ClassId == classId && u.Role == UserRole.Learner && u.ClassApproved)
            .Select(u => new { u.UserId, u.FirstName, u.LastName, u.Username, u.LpPoints, u.Streak, u.LastGoalDate })
            .ToListAsync();
        var ids = users.Select(u => u.UserId).ToList();

        var today = DailyProgressService.Today;
        var weekFrom = today.AddDays(-6);
        var week = (await db.UserDailyProgresses.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.Date >= weekFrom && p.Date <= today)
            .Select(p => new { p.UserId, p.WordsCount })
            .ToListAsync()).ToLookup(p => p.UserId);

        var lastActive = (await db.UserDailyProgresses.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.WordsCount > 0)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Last = g.Max(p => p.Date) })
            .ToListAsync()).ToDictionary(x => x.UserId, x => x.Last);

        // Only exams taken as a member of this class; earlier ones belong to the previous class.
        var exams = (await db.ExamResults.AsNoTracking()
            .Where(e => ids.Contains(e.UserId) && e.ClassId == classId)
            .OrderBy(e => e.TakenAt)
            .Select(e => new { e.UserId, e.TotalCount, e.CorrectCount, e.TakenAt })
            .ToListAsync()).ToLookup(e => e.UserId);

        var books = await StudentProgress.CurrentBooksAsync(db, ids);

        return users.Select(u =>
        {
            var w = week[u.UserId].ToList();
            var ex = exams[u.UserId].Where(e => e.TotalCount > 0).ToList();
            var streakAlive = u.LastGoalDate is { } last && last >= today.AddDays(-1);
            return new ClassStudentDto
            {
                UserId = u.UserId,
                Name = $"{u.FirstName} {u.LastName}".Trim(),
                Username = u.Username,
                LpPoints = u.LpPoints,
                Streak = streakAlive ? u.Streak : 0,
                WordsWeek = w.Sum(x => x.WordsCount),
                ActiveDaysWeek = w.Count(x => x.WordsCount > 0),
                LastActiveDate = lastActive.TryGetValue(u.UserId, out var d) ? d : null,
                ExamCount = ex.Count,
                LastExamPercent = ex.Count > 0 ? Percent(ex[^1].CorrectCount, ex[^1].TotalCount) : null,
                LastExamAt = ex.Count > 0 ? ex[^1].TakenAt : null,
                CurrentBook = books.TryGetValue(u.UserId, out var book) ? book : null,
                AvgExamPercent = ex.Count > 0 ? (int)Math.Round(ex.Average(e => e.CorrectCount * 100.0 / e.TotalCount)) : null
            };
        })
        .OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();
    }

    public async Task<List<PendingStudentDto>> GetPendingAsync(int classId)
    {
        var rows = await db.Users.AsNoTracking()
            .Where(u => u.ClassId == classId && u.Role == UserRole.Learner && !u.ClassApproved)
            .Select(u => new { u.UserId, u.FirstName, u.LastName, u.Username })
            .ToListAsync();
        return rows
            .Select(u => new PendingStudentDto { UserId = u.UserId, Name = $"{u.FirstName} {u.LastName}".Trim(), Username = u.Username })
            .OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int Percent(int correct, int total) => (int)Math.Round(correct * 100.0 / total);
}
