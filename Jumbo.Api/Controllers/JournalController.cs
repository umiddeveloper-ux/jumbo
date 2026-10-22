using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>
/// The teacher's journal of topics ("Журнал сдачи тем"): which pupil of a team has passed which topic of a book (15 words, said orally to the
/// teacher), with the date. A teacher works with the teams he runs, a director with every team of his school.
/// </summary>
[ApiController]
[Route("api/teacher/journal")]
[RequireRole(UserRole.Teacher, UserRole.Director)]
public class JournalController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    public const int WordsPerTopic = 15;

    [HttpGet("{classId:int}")]
    public async Task<ActionResult<JournalDto>> Get(int classId, [FromQuery] int book = 1)
    {
        var cls = await FindClassAsync(classId);
        if (cls is null) return NotFound();

        var books = await db.Subjects.AsNoTracking().Select(s => s.BookId).Distinct().OrderBy(b => b).ToListAsync();
        if (books.Count == 0) return Conflict(new { error = "NO_BOOKS" });
        if (!books.Contains(book)) book = books[0];

        var subjects = await db.Subjects.AsNoTracking().Where(s => s.BookId == book).OrderBy(s => s.SequenceNumber)
            .Select(s => new { s.SubjectId, s.SequenceNumber, s.SubjectName }).ToListAsync();
        var subjectIds = subjects.Select(s => s.SubjectId).ToList();

        // the approved learners of the team, by name
        var pupils = await db.Users.AsNoTracking().Where(u => u.ClassId == classId && u.ClassApproved)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new JournalPupilDto { UserId = u.UserId, Name = ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim() }).ToListAsync();
        var pupilIds = pupils.Select(p => p.UserId).ToList();

        var marks = await db.JournalMarks.AsNoTracking().Where(m => pupilIds.Contains(m.UserId) && subjectIds.Contains(m.SubjectId))
            .Select(m => new JournalMarkDto { UserId = m.UserId, SubjectId = m.SubjectId, PassedOn = m.PassedOn }).ToListAsync();

        var progress = await db.UserSubjectProgresses.AsNoTracking().Where(p => pupilIds.Contains(p.UserId) && subjectIds.Contains(p.SubjectId)).ToListAsync();
        var appPassed = progress.Where(p => p.AllWon).Select(p => new JournalMarkDto { UserId = p.UserId, SubjectId = p.SubjectId }).ToList();

        var school = await db.Schools.AsNoTracking().Where(s => s.SchoolId == cls.SchoolId).Select(s => s.Name).SingleOrDefaultAsync();
        var teacher = await db.Users.AsNoTracking().Where(u => u.UserId == cls.TeacherId)
            .Select(u => ((u.FirstName ?? "") + " " + (u.LastName ?? "")).Trim()).SingleOrDefaultAsync();

        int firstWord = (book - 1) * 300 + 1;
        var today = DateTime.UtcNow.AddHours(5);
        int startYear = today.Month >= 9 ? today.Year : today.Year - 1;          // the school year starts in September

        return new JournalDto
        {
            ClassId = cls.ClassId, ClassName = cls.Name, SchoolName = school ?? string.Empty, TeacherName = teacher,
            Book = book, Level = LanguageLevels.ForBook(book), WordFrom = firstWord, WordTo = firstWord + 299, SchoolYear = $"{startYear}–{startYear + 1}",
            Books = books,
            Topics = subjects.Select(s => new JournalTopicDto
            {
                SubjectId = s.SubjectId, Number = s.SequenceNumber, Name = s.SubjectName,
                WordFrom = firstWord + (s.SequenceNumber - 1) * WordsPerTopic, WordTo = firstWord + s.SequenceNumber * WordsPerTopic - 1
            }).ToList(),
            Pupils = pupils, Marks = marks, AppPassed = appPassed
        };
    }

    /// <summary>Puts a mark or takes it away. Only for an approved learner of one of the teacher's teams.</summary>
    [HttpPut("mark")]
    public async Task<ActionResult<JournalMarkDto?>> SetMark(SetJournalMarkRequest request)
    {
        var cls = await FindClassAsync(request.ClassId);
        if (cls is null) return NotFound();

        var pupilOk = await db.Users.AsNoTracking().AnyAsync(u => u.UserId == request.PupilId && u.ClassId == cls.ClassId && u.ClassApproved);
        if (!pupilOk) return NotFound(new { error = "NO_PUPIL" });
        if (!await db.Subjects.AsNoTracking().AnyAsync(s => s.SubjectId == request.SubjectId)) return NotFound(new { error = "NO_TOPIC" });

        var mark = await db.JournalMarks.SingleOrDefaultAsync(m => m.UserId == request.PupilId && m.SubjectId == request.SubjectId);
        if (request.PassedOn is not { } date)
        {
            if (mark is not null) { db.JournalMarks.Remove(mark); await db.SaveChangesAsync(); }
            return Ok(null);
        }

        // a date in the far future or the distant past is a slip of the finger
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        if (date > today.AddDays(1) || date < today.AddYears(-6)) return BadRequest(new { error = "BAD_DATE" });

        var teacherId = (await currentUser.GetUserIdAsync())!.Value;
        if (mark is null) { mark = new JournalMark { UserId = request.PupilId, SubjectId = request.SubjectId }; db.JournalMarks.Add(mark); }
        mark.PassedOn = date;
        mark.TeacherId = teacherId;
        await db.SaveChangesAsync();
        return new JournalMarkDto { UserId = mark.UserId, SubjectId = mark.SubjectId, PassedOn = mark.PassedOn };
    }

    // a teacher's own team, any team of the school for a director
    private async Task<SchoolClass?> FindClassAsync(int classId)
    {
        var meId = (await currentUser.GetUserIdAsync())!.Value;
        var me = await db.Users.AsNoTracking().Where(u => u.UserId == meId).Select(u => new { u.Role, u.SchoolId }).SingleAsync();
        if (me.SchoolId is null) return null;
        var q = me.Role == UserRole.Director
            ? db.SchoolClasses.AsNoTracking().Where(c => c.SchoolId == me.SchoolId)
            : db.SchoolClasses.AsNoTracking().Where(c => c.TeacherId == meId);
        return await q.SingleOrDefaultAsync(c => c.ClassId == classId);
    }
}
