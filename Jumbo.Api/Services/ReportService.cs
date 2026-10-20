using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Team and school reports. Only learners a teacher has approved are counted; days are app-time days (UTC+5).
public class ReportService(AppDbContext db)
{
    private const int TopMissed = 10;

    public static ReportPeriodDto MakePeriod(string? key)
    {
        var month = string.Equals(key, "month", StringComparison.OrdinalIgnoreCase);
        var days = month ? 30 : 7;
        var to = DailyProgressService.Today;
        var from = to.AddDays(-(days - 1));
        var prevTo = from.AddDays(-1);
        return new ReportPeriodDto { Key = month ? "month" : "week", From = from, To = to, PrevFrom = prevTo.AddDays(-(days - 1)), PrevTo = prevTo };
    }

    public async Task<TeamReportDto> BuildTeamAsync(SchoolClass cls, string? periodKey)
    {
        var period = MakePeriod(periodKey);
        var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == cls.SchoolId);
        var teacher = await db.Users.AsNoTracking().Where(u => u.UserId == cls.TeacherId)
            .Select(u => new { u.FirstName, u.LastName }).SingleOrDefaultAsync();

        var data = await LoadAsync([cls.ClassId], period);
        var ids = data.Members.Select(m => m.UserId).ToList();

        var lastActive = (await db.UserDailyProgresses.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.WordsCount > 0)
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Last = g.Max(p => p.Date) })
            .ToListAsync()).ToDictionary(x => x.UserId, x => x.Last);

        var books = await StudentProgress.CurrentBooksAsync(db, ids);
        var today = DailyProgressService.Today;
        var goodDays = period.Key == "month" ? 8 : 3;
        var students = data.Members.Select(m =>
        {
            var cur = data.Progress[m.UserId].Where(p => p.Date >= period.From && p.Date <= period.To && p.Words > 0).ToList();
            var ex = data.Exams.Where(e => e.UserId == m.UserId && InRange(e.Day, period.From, period.To)).ToList();
            var streakAlive = m.LastGoalDate is { } last && last >= today.AddDays(-1);
            return new ReportStudentDto
            {
                UserId = m.UserId,
                Name = m.Name,
                Words = cur.Sum(p => p.Words),
                ActiveDays = cur.Count,
                LastActiveDate = lastActive.TryGetValue(m.UserId, out var d) ? d : null,
                Streak = streakAlive ? m.Streak : 0,
                ExamCount = ex.Count,
                AvgExamPercent = AvgPercent(ex),
                CurrentBook = books.TryGetValue(m.UserId, out var book) ? book : null,
                Status = cur.Count == 0 ? "absent" : cur.Count >= goodDays ? "active" : "slow"
            };
        })
        .OrderByDescending(s => s.Words)
        .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
        .ToList();

        return new TeamReportDto
        {
            TeamName = cls.Name,
            SchoolName = school.Name,
            Region = school.Region,
            TeacherName = teacher is null ? null : $"{teacher.FirstName} {teacher.LastName}".Trim(),
            Period = period,
            Totals = Totals(ids, period, data),
            Students = students,
            MissedWords = await MissedWordsAsync(data, period),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<SchoolReportDto> BuildSchoolAsync(School school, string? periodKey)
    {
        var period = MakePeriod(periodKey);
        var classes = await db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolId == school.SchoolId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync();
        var data = await LoadAsync(classes.Select(c => c.ClassId).ToList(), period);

        var teacherIds = classes.Select(c => c.TeacherId).Distinct().ToList();
        var teachers = await db.Users.AsNoTracking().Where(u => teacherIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FirstName, u.LastName }).ToDictionaryAsync(u => u.UserId);

        var rows = classes.Select(c =>
        {
            var ids = data.Members.Where(m => m.ClassId == c.ClassId).Select(m => m.UserId).ToList();
            teachers.TryGetValue(c.TeacherId, out var t);
            return new ReportTeamRowDto
            {
                TeamName = c.Name,
                TeacherName = t is null ? null : $"{t.FirstName} {t.LastName}".Trim(),
                Totals = Totals(ids, period, data, c.ClassId)
            };
        })
        .OrderByDescending(r => r.Totals.Words)
        .ToList();

        return new SchoolReportDto
        {
            SchoolName = school.Name,
            Region = school.Region,
            Period = period,
            Totals = Totals(data.Members.Select(m => m.UserId).ToList(), period, data),
            Teams = rows,
            GeneratedAt = DateTime.UtcNow
        };
    }

    // ── data ────────────────────────────────────────────────────────────────
    private sealed record Member(int UserId, int ClassId, string Name, int Streak, DateOnly? LastGoalDate);
    private sealed record Day(DateOnly Date, int Words);
    private sealed record Exam(int ResultId, int UserId, int ClassId, DateOnly Day, int Total, int Correct);

    private sealed class Data
    {
        public List<Member> Members { get; init; } = new();
        public ILookup<int, Day> Progress { get; init; } = null!;
        public List<Exam> Exams { get; init; } = new();
    }

    private async Task<Data> LoadAsync(IReadOnlyCollection<int> classIds, ReportPeriodDto period)
    {
        var members = (await db.Users.AsNoTracking()
            .Where(u => u.ClassId != null && classIds.Contains(u.ClassId.Value) && u.Role == UserRole.Learner && u.ClassApproved)
            .Select(u => new { u.UserId, ClassId = u.ClassId!.Value, u.FirstName, u.LastName, u.Streak, u.LastGoalDate })
            .ToListAsync())
            .Select(u => new Member(u.UserId, u.ClassId, $"{u.FirstName} {u.LastName}".Trim(), u.Streak, u.LastGoalDate))
            .ToList();
        var ids = members.Select(m => m.UserId).ToList();

        var progress = (await db.UserDailyProgresses.AsNoTracking()
            .Where(p => ids.Contains(p.UserId) && p.Date >= period.PrevFrom && p.Date <= period.To)
            .Select(p => new { p.UserId, p.Date, p.WordsCount })
            .ToListAsync())
            .ToLookup(p => p.UserId, p => new Day(p.Date, p.WordsCount));

        // Exams taken in the team (the snapshot), by its approved members; a few hours of slack for the UTC+5 day.
        var since = DateTime.SpecifyKind(period.PrevFrom.ToDateTime(TimeOnly.MinValue).AddHours(-6), DateTimeKind.Utc);
        var exams = (await db.ExamResults.AsNoTracking()
            .Where(e => ids.Contains(e.UserId) && e.ClassId != null && classIds.Contains(e.ClassId.Value) && e.TakenAt >= since && e.TotalCount > 0)
            .Select(e => new { e.ExamResultId, e.UserId, ClassId = e.ClassId!.Value, e.TakenAt, e.TotalCount, e.CorrectCount })
            .ToListAsync())
            .Select(e => new Exam(e.ExamResultId, e.UserId, e.ClassId, DateOnly.FromDateTime(e.TakenAt.AddHours(5)), e.TotalCount, e.CorrectCount))
            .Where(e => InRange(e.Day, period.PrevFrom, period.To))
            .ToList();

        return new Data { Members = members, Progress = progress, Exams = exams };
    }

    private ReportTotalsDto Totals(IReadOnlyCollection<int> ids, ReportPeriodDto period, Data data, int? onlyClassId = null)
    {
        (int Active, int Words, List<Exam> Exams) Window(DateOnly from, DateOnly to)
        {
            var days = ids.SelectMany(id => data.Progress[id].Where(p => InRange(p.Date, from, to)).Select(p => (id, p))).ToList();
            var active = days.Where(x => x.p.Words > 0).Select(x => x.id).Distinct().Count();
            var exams = data.Exams.Where(e => ids.Contains(e.UserId) && InRange(e.Day, from, to) && (onlyClassId is null || e.ClassId == onlyClassId)).ToList();
            return (active, days.Sum(x => x.p.Words), exams);
        }

        var cur = Window(period.From, period.To);
        var prev = Window(period.PrevFrom, period.PrevTo);
        return new ReportTotalsDto
        {
            Students = ids.Count,
            Active = cur.Active,
            Words = cur.Words,
            ExamCount = cur.Exams.Count,
            AvgExamPercent = AvgPercent(cur.Exams),
            ActivePrev = prev.Active,
            WordsPrev = prev.Words,
            ExamCountPrev = prev.Exams.Count,
            AvgExamPercentPrev = AvgPercent(prev.Exams)
        };
    }

    private async Task<List<MissedWordDto>> MissedWordsAsync(Data data, ReportPeriodDto period)
    {
        var resultIds = data.Exams.Where(e => InRange(e.Day, period.From, period.To)).Select(e => e.ResultId).ToList();
        if (resultIds.Count == 0) return new();

        var rows = await db.ExamMistakes.AsNoTracking()
            .Where(m => resultIds.Contains(m.ExamResultId))
            .GroupBy(m => m.Word.ToLower())
            .Select(g => new { Word = g.Key, Translation = g.Max(m => m.Translation), Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Word)
            .Take(TopMissed)
            .ToListAsync();
        return rows.Select(r => new MissedWordDto { Word = r.Word, Translation = r.Translation, Count = r.Count }).ToList();
    }

    private static bool InRange(DateOnly d, DateOnly from, DateOnly to) => d >= from && d <= to;

    private static int? AvgPercent(IReadOnlyCollection<Exam> exams) =>
        exams.Count == 0 ? null : (int)Math.Round(exams.Average(e => e.Correct * 100.0 / e.Total));
}
