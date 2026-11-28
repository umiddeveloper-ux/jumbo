using System.Security.Cryptography;
using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// Exams a teacher starts for their pupils: the teacher picks the number of questions and gets a 6-digit code that works
// for CodeLifetime. Pupils enter it on the exam page, pick a book and take the exam; each attempt is stored as an
// ExamResult linked to the TeacherExam (date, book, right / wrong answers).
[ApiController]
[Route("api/teacher/exams")]
[RequireRole(UserRole.Teacher, UserRole.Director)]
public class TeacherExamsController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    private const int MinQuestions = 5;
    private const int MaxQuestions = 100;
    private const int HistoryLimit = 30;
    public const int DefaultSecondsPerWord = 30;
    private const int MinSeconds = 5;
    private const int MaxSeconds = 600;

    [HttpPost]
    public async Task<ActionResult<TeacherExamSummaryDto>> Create(CreateTeacherExamRequest request)
    {
        if (request.WordCount < MinQuestions || request.WordCount > MaxQuestions)
            return BadRequest(new { error = "INVALID_COUNT" });

        var seconds = request.SecondsPerWord <= 0 ? DefaultSecondsPerWord : request.SecondsPerWord;
        if (seconds < MinSeconds || seconds > MaxSeconds)
            return BadRequest(new { error = "INVALID_SECONDS" });

        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var now = DateTime.UtcNow;

        // A code is unique among the codes that still work, so what a pupil types always means one exam.
        string? code = null;
        for (var i = 0; i < 30 && code is null; i++)
        {
            var candidate = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            if (!await db.TeacherExams.AnyAsync(e => e.Code == candidate && e.ExpiresAt > now))
                code = candidate;
        }
        if (code is null) return Conflict(new { error = "TRY_AGAIN" });

        var exam = new TeacherExam
        {
            TeacherId = me.UserId,
            Code = code,
            WordCount = request.WordCount,
            SecondsPerWord = seconds,
            CreatedAt = now,
            ExpiresAt = now.Add(CodeLifetime)
        };
        db.TeacherExams.Add(exam);
        await db.SaveChangesAsync();

        return Summary(exam, []);
    }

    // The teacher's latest exams, newest first.
    [HttpGet]
    public async Task<ActionResult<List<TeacherExamSummaryDto>>> List()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var exams = await db.TeacherExams.AsNoTracking()
            .Where(e => e.TeacherId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(HistoryLimit)
            .ToListAsync();

        var ids = exams.Select(e => (int?)e.TeacherExamId).ToList();
        var rows = await db.ExamResults.AsNoTracking()
            .Where(r => ids.Contains(r.TeacherExamId))
            .Select(r => new { r.TeacherExamId, r.TotalCount, r.CorrectCount })
            .ToListAsync();
        var byExam = rows.GroupBy(r => r.TeacherExamId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(r => (r.TotalCount, r.CorrectCount)).ToList());

        return exams.Select(e => Summary(e, byExam.GetValueOrDefault(e.TeacherExamId) ?? [])).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TeacherExamDetailDto>> Detail(int id)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var exam = await db.TeacherExams.AsNoTracking().SingleOrDefaultAsync(e => e.TeacherExamId == id && e.TeacherId == userId);
        if (exam is null) return NotFound();

        var rows = await (
            from r in db.ExamResults.AsNoTracking()
            where r.TeacherExamId == id
            join u in db.Users.AsNoTracking() on r.UserId equals u.UserId
            join c in db.SchoolClasses.AsNoTracking() on r.ClassId equals c.ClassId into cs
            from c in cs.DefaultIfEmpty()
            orderby r.TakenAt descending
            select new TeacherExamResultDto
            {
                UserId = r.UserId,
                Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                ClassName = c == null ? null : c.Name,
                BookNumber = r.BookNumber,
                TotalCount = r.TotalCount,
                CorrectCount = r.CorrectCount,
                TakenAt = r.TakenAt,
                ByCode = true
            }).ToListAsync();

        return new TeacherExamDetailDto
        {
            Exam = Summary(exam, rows.Select(r => (r.TotalCount, r.CorrectCount)).ToList()),
            // the best result first
            Results = rows.OrderByDescending(r => r.Percent).ThenByDescending(r => r.CorrectCount)
                .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase).ToList()
        };
    }

    // Every exam of the teacher's pupils (a Director: of the whole school), free ones and exams by code, newest first.
    // "Team" is the pupil's team at the moment of the exam; classId narrows it to one team.
    [HttpGet("results")]
    public async Task<ActionResult<List<TeacherExamResultDto>>> AllResults([FromQuery] int? classId = null, [FromQuery] int take = 300)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var classes = me.Role == UserRole.Director
            ? db.SchoolClasses.AsNoTracking().Where(c => c.SchoolId == me.SchoolId)
            : db.SchoolClasses.AsNoTracking().Where(c => c.TeacherId == me.UserId);
        if (classId is { } only) classes = classes.Where(c => c.ClassId == only);
        take = Math.Clamp(take, 1, 1000);

        return await (
            from r in db.ExamResults.AsNoTracking()
            join c in classes on r.ClassId equals c.ClassId
            join u in db.Users.AsNoTracking() on r.UserId equals u.UserId
            where r.TotalCount > 0 && u.ClassApproved && u.ClassId == r.ClassId
            orderby r.TakenAt descending
            select new TeacherExamResultDto
            {
                UserId = r.UserId,
                Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim(),
                ClassName = c.Name,
                BookNumber = r.BookNumber,
                TotalCount = r.TotalCount,
                CorrectCount = r.CorrectCount,
                TakenAt = r.TakenAt,
                ByCode = r.TeacherExamId != null
            }).Take(take).ToListAsync();
    }

    // The teams of the teacher (a Director: of the whole school) with the average of the pupils' best results in the exams by code.
    [HttpGet("teams")]
    public async Task<ActionResult<List<ExamTeamDto>>> Teams()
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var classes = (await MyClasses(me).ToListAsync()).OrderBy(c => c.IsArchived).ThenBy(c => c.Name, Jumbo.Shared.TeamNames.Natural).ToList();
        var ids = classes.Select(c => c.ClassId).ToList();

        var pupils = await db.Users.AsNoTracking()
            .Where(u => u.ClassApproved && u.ClassId != null && ids.Contains(u.ClassId.Value))
            .Select(u => new { u.UserId, ClassId = u.ClassId!.Value })
            .ToListAsync();
        var best = await ExamStats.BestByPupilAndBookAsync(db, ids);

        return classes.Select(c =>
        {
            var team = pupils.Where(p => p.ClassId == c.ClassId).Select(p => p.UserId).ToList();
            // each pupil's best result over all the books
            var bests = team.Where(u => best.ContainsKey(u)).Select(u => best[u].Values.Max()).ToList();
            return new ExamTeamDto
            {
                ClassId = c.ClassId,
                Name = c.Name,
                IsArchived = c.IsArchived,
                PupilCount = team.Count,
                ExaminedCount = bests.Count,
                AvgBestPercent = bests.Count == 0 ? null : (int)Math.Round(bests.Average() * 100)
            };
        }).ToList();
    }

    // One team: every pupil and, per book, the best result of the exams by code and the words learned (300 x that result).
    [HttpGet("teams/{classId:int}")]
    public async Task<ActionResult<ExamTeamReportDto>> Team(int classId)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var cls = await MyClasses(me).SingleOrDefaultAsync(c => c.ClassId == classId);
        if (cls is null) return NotFound();

        var pupils = await db.Users.AsNoTracking()
            .Where(u => u.ClassApproved && u.ClassId == classId)
            .Select(u => new { u.UserId, Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() })
            .ToListAsync();
        var best = await ExamStats.BestByPupilAndBookAsync(db, [classId]);

        var report = new ExamTeamReportDto { ClassId = cls.ClassId, Name = cls.Name };
        foreach (var p in pupils.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var row = new ExamPupilRowDto { UserId = p.UserId, Name = p.Name };
            best.TryGetValue(p.UserId, out var byBook);
            for (var b = 1; b <= Jumbo.Shared.Course.BookCount; b++)
            {
                if (byBook is not null && byBook.TryGetValue(b, out var ratio))
                {
                    row.Percents.Add((int)Math.Round(ratio * 100));
                    row.Words.Add(ExamStats.WordsOf(ratio));
                }
                else { row.Percents.Add(null); row.Words.Add(null); }
            }
            report.Pupils.Add(row);
        }
        // the pupil with the most words learned (the "total" column) first
        report.Pupils = report.Pupils.OrderByDescending(p => p.Words.Sum(w => w ?? 0))
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        return report;
    }

    private IQueryable<SchoolClass> MyClasses(User me) => me.Role == UserRole.Director
        ? db.SchoolClasses.AsNoTracking().Where(c => c.SchoolId == me.SchoolId)
        : db.SchoolClasses.AsNoTracking().Where(c => c.TeacherId == me.UserId);


    private static TeacherExamSummaryDto Summary(TeacherExam e, List<(int Total, int Correct)> results) => new()
    {
        TeacherExamId = e.TeacherExamId,
        Code = e.Code,
        WordCount = e.WordCount,
        SecondsPerWord = e.SecondsPerWord,
        CreatedAt = e.CreatedAt,
        ExpiresAt = e.ExpiresAt,
        ParticipantCount = results.Count,
        AvgPercent = results.Count == 0
            ? null
            : (int)Math.Round(results.Average(x => x.Total > 0 ? x.Correct * 100.0 / x.Total : 0))
    };

    private async Task<User> GetMeAsync()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
    }
}
