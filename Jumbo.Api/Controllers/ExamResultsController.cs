using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jumbo.Api.Controllers;

// Finished exams, stored for the teacher's and director's reports.
[ApiController]
[Route("api/exam-results")]
[RequireAccessToken]
public class ExamResultsController(AppDbContext db, ICurrentUserService currentUser, IMemoryCache cache) : ControllerBase
{
    private const int MaxQuestions = 200;
    private const int MinQuestions = 1;

    // A pupil may still hand in a teacher's exam this long after it was created (the code itself works for 5 minutes,
    // the exam takes a while).
    private static readonly TimeSpan FinishWindow = TimeSpan.FromHours(3);
    private const int MaxWrongCodesPerMinute = 10;

    // A pupil types the teacher's 6-digit code: returns the exam (how many questions) or why the code does not work.
    [HttpPost("join")]
    public async Task<ActionResult<JoinTeacherExamResponse>> Join(JoinTeacherExamRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        // 6 digits are guessable in a few minutes, so wrong codes are rationed.
        var key = $"exam-code-fails:{userId}";
        if (cache.TryGetValue(key, out int fails) && fails >= MaxWrongCodesPerMinute)
            return StatusCode(429, new { error = "TOO_MANY" });

        var code = request.Code?.Trim() ?? string.Empty;
        var now = DateTime.UtcNow;
        var exam = code.Length == 6 && code.All(char.IsAsciiDigit)
            ? await db.TeacherExams.AsNoTracking()
                .Where(e => e.Code == code && e.ExpiresAt > now)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync()
            : null;

        if (exam is null)
        {
            cache.Set(key, fails + 1, TimeSpan.FromMinutes(1));
            // Was it a real code that just ran out? Tell the pupil (the teacher can start a new exam).
            var expired = code.Length == 6 && await db.TeacherExams.AsNoTracking()
                .AnyAsync(e => e.Code == code && e.ExpiresAt <= now && e.ExpiresAt > now.AddHours(-1));
            return NotFound(new { error = expired ? "CODE_EXPIRED" : "CODE_INVALID" });
        }

        if (await db.ExamResults.AnyAsync(r => r.UserId == userId && r.TeacherExamId == exam.TeacherExamId))
            return Conflict(new { error = "ALREADY_TAKEN" });

        return new JoinTeacherExamResponse { TeacherExamId = exam.TeacherExamId, WordCount = exam.WordCount };
    }

    // Free exams are no longer stored (the pupil sees the result at the end and that is all), so there is nothing to show here any more.
    // (The route stays for the apps and pages of an older version.)
    [HttpGet("mine")]
    public ActionResult<List<MyExamBookResultDto>> GetMine() => new List<MyExamBookResultDto>();

    [HttpPost]
    public async Task<IActionResult> Submit(SubmitExamResultRequest request)
    {
        // A free exam is not stored: only the exams taken with a teacher's code are (they feed the teacher's reports).
        if (request.TeacherExamId is null) return NoContent();

        if (request.TotalCount < MinQuestions || request.TotalCount > MaxQuestions
            || request.CorrectCount < 0 || request.CorrectCount > request.TotalCount
            || request.BookNumber < 1 || request.BookNumber > 100)
            return BadRequest(new { error = "Invalid exam result." });

        var userId = (await currentUser.GetUserIdAsync())!.Value;

        if (request.TeacherExamId is { } examId)
        {
            var exam = await db.TeacherExams.AsNoTracking().SingleOrDefaultAsync(e => e.TeacherExamId == examId);
            if (exam is null || DateTime.UtcNow > exam.CreatedAt + FinishWindow)
                return BadRequest(new { error = "EXAM_CLOSED" });
            if (request.TotalCount > exam.WordCount)
                return BadRequest(new { error = "Invalid exam result." });
            if (await db.ExamResults.AnyAsync(r => r.UserId == userId && r.TeacherExamId == examId))
                return Conflict(new { error = "ALREADY_TAKEN" });
        }

        var classId = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.ClassId).SingleAsync();

        var result = new ExamResult
        {
            UserId = userId,
            ClassId = classId,
            BookNumber = request.BookNumber,
            TeacherExamId = request.TeacherExamId,
            TotalCount = request.TotalCount,
            CorrectCount = request.CorrectCount
        };
        db.ExamResults.Add(result);
        await db.SaveChangesAsync();

        // The wrong words feed the team report. Capped: a result can never have more mistakes than questions.
        var mistakes = (request.Mistakes ?? new())
            .Where(m => !string.IsNullOrWhiteSpace(m.Word))
            .Take(request.TotalCount - request.CorrectCount)
            .Select(m => new ExamMistake
            {
                ExamResultId = result.ExamResultId,
                Word = Clip(m.Word, 100)!,
                Translation = Clip(m.Translation, 100)
            })
            .ToList();
        if (mistakes.Count > 0)
        {
            db.ExamMistakes.AddRange(mistakes);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }

    private static string? Clip(string? value, int max)
    {
        var v = value?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        return v.Length > max ? v[..max] : v;
    }
}
