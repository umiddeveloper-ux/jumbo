using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// The director's exam report: the exams of the whole school by teams, from the exams taken with a teacher's code (the same data and the same
// rules as the teacher's team exam report: approved pupils of a team, whose team is still the team of the exam). Days are app-days (UTC+5).
public class ExamReportService(AppDbContext db)
{
    private static readonly TimeSpan AppOffset = TimeSpan.FromHours(5);

    public static ExamPeriodDto MakePeriod(string? key, DateOnly? today = null)
    {
        var k = ExamReportPeriods.Normalize(key);
        var to = today ?? DailyProgressService.Today;
        return new ExamPeriodDto { Key = k, From = ExamReportPeriods.From(k, to), To = to };
    }

    private sealed record Attempt(int ExamId, int UserId, int ClassId, DateTime TakenAt, DateOnly Day, int Total, int Correct)
    {
        public double Percent => Correct * 100.0 / Total;
    }

    public async Task<SchoolExamReportDto> BuildSchoolAsync(School school, string? periodKey, DateOnly? today = null)
    {
        var period = MakePeriod(periodKey, today);

        var classes = await db.SchoolClasses.AsNoTracking()
            .Where(c => c.SchoolId == school.SchoolId && !c.IsArchived)
            .OrderBy(c => c.Name)
            .ToListAsync();
        var classIds = classes.Select(c => c.ClassId).ToList();

        var pupils = await db.Users.AsNoTracking()
            .Where(u => u.ClassApproved && u.ClassId != null && classIds.Contains(u.ClassId.Value))
            .Select(u => new { u.UserId, ClassId = u.ClassId!.Value, Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() })
            .ToListAsync();
        var pupilIds = pupils.Select(p => p.UserId).ToList();

        // all-time best result per pupil and book: the "words learned" column (the same helper as the teacher's report)
        var best = await ExamStats.BestByPupilAndBookAsync(db, classIds);

        // the attempts of the period (a little slack in the query for the UTC+5 day, the exact day is checked below)
        var since = DateTime.SpecifyKind(period.From.ToDateTime(TimeOnly.MinValue) - AppOffset - TimeSpan.FromHours(1), DateTimeKind.Utc);
        var attempts = (await (
                from r in db.ExamResults.AsNoTracking()
                join u in db.Users.AsNoTracking() on r.UserId equals u.UserId
                where r.TeacherExamId != null && r.TotalCount > 0 && r.ClassId != null && classIds.Contains(r.ClassId.Value)
                      && u.ClassApproved && u.ClassId == r.ClassId && r.TakenAt >= since
                select new { r.TeacherExamId, r.UserId, ClassId = r.ClassId!.Value, r.TakenAt, r.TotalCount, r.CorrectCount }).ToListAsync())
            .Select(r => new Attempt(r.TeacherExamId!.Value, r.UserId, r.ClassId, r.TakenAt, DayOf(r.TakenAt), r.TotalCount, r.CorrectCount))
            .Where(a => a.Day >= period.From && a.Day <= period.To)
            .ToList();

        var examIds = attempts.Select(a => a.ExamId).Distinct().ToList();
        var exams = await db.TeacherExams.AsNoTracking().Where(e => examIds.Contains(e.TeacherExamId)).ToDictionaryAsync(e => e.TeacherExamId);
        var teacherIds = classes.Select(c => c.TeacherId).Concat(exams.Values.Select(e => e.TeacherId)).Distinct().ToList();
        var teachers = await db.Users.AsNoTracking().Where(u => teacherIds.Contains(u.UserId))
            .Select(u => new { u.UserId, Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() })
            .ToDictionaryAsync(u => u.UserId, u => u.Name);
        string? TeacherName(int id) => teachers.TryGetValue(id, out var n) && n.Length > 0 ? n : null;

        var teams = new List<ExamReportTeamDto>();
        foreach (var c in classes)
        {
            var team = pupils.Where(p => p.ClassId == c.ClassId).ToList();
            var mine = attempts.Where(a => a.ClassId == c.ClassId).ToList();

            var rows = team.Select(p =>
            {
                var own = mine.Where(a => a.UserId == p.UserId).OrderBy(a => a.TakenAt).ToList();
                var last = own.Count == 0 ? null : own[^1];
                best.TryGetValue(p.UserId, out var byBook);
                return new ExamReportPupilDto
                {
                    Name = p.Name,
                    Exams = own.Count,
                    BestPercent = own.Count == 0 ? null : own.Max(a => Round(a.Percent)),
                    LastPercent = last is null ? null : Round(last.Percent),
                    LastDate = last?.Day,
                    WordsTotal = ExamStats.WordsTotal(byBook)
                };
            })
            // the pupils who took an exam first, the best first; then the others
            .OrderByDescending(r => r.Exams > 0)
            .ThenByDescending(r => r.BestPercent ?? -1)
            .ThenByDescending(r => r.WordsTotal)
            .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

            var heldExams = mine.GroupBy(a => a.ExamId).Where(g => exams.ContainsKey(g.Key))
                .Select(g =>
                {
                    var e = exams[g.Key];
                    return new ExamReportExamDto
                    {
                        Date = DayOf(e.CreatedAt),
                        TeacherName = TeacherName(e.TeacherId),
                        Questions = e.WordCount,
                        Participants = g.Select(a => a.UserId).Distinct().Count(),
                        AvgPercent = AvgPercent(g.ToList())
                    };
                })
                .OrderBy(e => e.Date).ThenBy(e => e.TeacherName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            teams.Add(new ExamReportTeamDto
            {
                TeamName = c.Name,
                TeacherName = TeacherName(c.TeacherId),
                Pupils = team.Count,
                Participants = mine.Select(a => a.UserId).Distinct().Count(),
                Attempts = mine.Count,
                AvgPercent = AvgPercent(mine),
                Exams = heldExams,
                PupilRows = rows
            });
        }

        return new SchoolExamReportDto
        {
            SchoolName = school.Name,
            Region = school.Region,
            Period = period,
            Totals = new ExamReportTotalsDto
            {
                Teams = teams.Count,
                Pupils = pupils.Count,
                Participants = attempts.Select(a => a.UserId).Distinct().Count(),
                ExamsHeld = examIds.Count,
                Attempts = attempts.Count,
                AvgPercent = AvgPercent(attempts)
            },
            Teams = teams,
            GeneratedAt = DateTime.UtcNow
        };
    }

    private static DateOnly DayOf(DateTime utc) => DateOnly.FromDateTime(utc + AppOffset);

    private static int Round(double percent) => (int)Math.Round(percent);

    private static int? AvgPercent(IReadOnlyCollection<Attempt> list) => list.Count == 0 ? null : Round(list.Average(a => a.Percent));
}
