using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Api.Services.Telegram;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Controllers;

// Teacher cabinet: teams ("команды"), the learners who picked them (approve / reject) and the approved learners'
// progress. A Teacher sees their own teams, a Director every team of their school. Both need to be linked to a School
// (done when an Admin approves the staff request).
[ApiController]
[Route("api/teacher")]
[RequireRole(UserRole.Teacher, UserRole.Director)]
public class TeacherController(AppDbContext db, ICurrentUserService currentUser, ClassStatsService stats, RatingService ratings, ReportService reports, IOptions<TelegramOptions> telegram, TelegramNotifier telegramNotifier) : ControllerBase
{
    private const int MaxClassesPerTeacher = 30;

    [HttpGet("classes")]
    public async Task<ActionResult<List<ClassDto>>> Classes([FromQuery] bool includeArchived = false)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var q = VisibleClasses(me);
        if (!includeArchived) q = q.Where(c => !c.IsArchived);
        var classes = await q.OrderBy(c => c.IsArchived).ThenBy(c => c.Name).ToListAsync();
        return await stats.GetClassesAsync(classes);
    }

    [HttpPost("classes")]
    public async Task<ActionResult<ClassDto>> Create(CreateClassRequest request)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name)) return BadRequest(new { error = "Team name is required." });
        if (name.Length > 60) name = name[..60];

        var mine = db.SchoolClasses.Where(c => c.TeacherId == me.UserId);
        if (await mine.CountAsync() >= MaxClassesPerTeacher) return Conflict(new { error = "TOO_MANY_CLASSES" });

        // One active team per name in a school: the learners choose by name from a list.
        if (await NameTakenAsync(me.SchoolId.Value, name, exceptId: null))
            return Conflict(new { error = "NAME_EXISTS" });

        var cls = new SchoolClass
        {
            SchoolId = me.SchoolId.Value,
            TeacherId = me.UserId,
            Name = name
        };
        db.SchoolClasses.Add(cls);
        await db.SaveChangesAsync();
        return (await stats.GetClassesAsync([cls]))[0];
    }

    // The leaderboard of the teacher's own pupils: one team (classId) or all of the teacher's teams (classId = 0; a Director
    // sees the whole school). Period: week | all.
    [HttpGet("rating")]
    public async Task<ActionResult<RatingDto>> Rating([FromQuery] int classId = 0, [FromQuery] string period = "week")
    {
        period = period.ToLowerInvariant();
        if (period is not ("week" or "all")) return BadRequest(new { error = "Unknown period." });

        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var classIds = VisibleClasses(me).Where(c => !c.IsArchived).Select(c => c.ClassId);
        if (classId != 0)
        {
            if (!await classIds.AnyAsync(id => id == classId)) return NotFound();
            classIds = classIds.Where(id => id == classId);
        }

        var pupilIds = await db.Users.AsNoTracking()
            .Where(u => u.ClassId != null && classIds.Contains(u.ClassId.Value) && u.Role == UserRole.Learner && u.ClassApproved)
            .Select(u => u.UserId)
            .ToListAsync();
        return await ratings.GetForPupilsAsync(pupilIds, period);
    }

    [HttpGet("classes/{id:int}")]
    public async Task<ActionResult<ClassDetailDto>> Detail(int id)
    {
        var cls = await FindOwnedAsync(id);
        if (cls is null) return NotFound();

        return new ClassDetailDto
        {
            Class = (await stats.GetClassesAsync([cls]))[0],
            Students = await stats.GetStudentsAsync(id),
            Pending = await stats.GetPendingAsync(id)
        };
    }

    // One approved pupil of the team: the full picture, book by book.
    [HttpGet("classes/{id:int}/students/{userId:int}")]
    public async Task<ActionResult<StudentProgressDto>> StudentProgress(int id, int userId)
    {
        var cls = await FindOwnedAsync(id);
        if (cls is null) return NotFound();

        var pupil = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.UserId == userId && u.ClassId == id && u.Role == UserRole.Learner && u.ClassApproved);
        if (pupil is null) return NotFound();

        return await Services.StudentProgress.BuildAsync(db, pupil, cls.Name);
    }

    [HttpPut("classes/{id:int}/archived")]
    public async Task<ActionResult<ClassDto>> SetArchived(int id, SetArchivedRequest request)
    {
        var cls = await FindOwnedAsync(id, track: true);
        if (cls is null) return NotFound();

        if (!request.IsArchived && await NameTakenAsync(cls.SchoolId, cls.Name, exceptId: cls.ClassId))
            return Conflict(new { error = "NAME_EXISTS" });

        cls.IsArchived = request.IsArchived;
        await db.SaveChangesAsync();
        return (await stats.GetClassesAsync([cls]))[0];
    }

    // Team report with pupil names: only for the team's teacher (or the school's director).
    [HttpGet("classes/{id:int}/report")]
    public async Task<ActionResult<TeamReportDto>> TeamReport(int id, [FromQuery] string period = "week")
    {
        var cls = await FindOwnedAsync(id);
        if (cls is null) return NotFound();
        return await reports.BuildTeamAsync(cls, period);
    }

    // The figures of all the teams of the teacher (a Director: of the school), one row per team and the total: the page "Reports".
    [HttpGet("reports/teams")]
    public async Task<ActionResult<SchoolReportDto>> TeamsReport([FromQuery] string period = "week")
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });
        var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == me.SchoolId);
        var classes = await VisibleClasses(me).Where(c => !c.IsArchived).OrderBy(c => c.Name).ToListAsync();
        return await reports.BuildTeamsAsync(school, classes, period, withClassIds: true);
    }

    // ── school report link (for the director, who does not log in) ──────────
    [HttpGet("school-report-link")]
    public async Task<ActionResult<ReportLinkDto>> GetSchoolLink()
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var link = await ActiveLinkAsync(me.SchoolId.Value);
        return link is null ? NotFound() : new ReportLinkDto { Token = link.Token, CreatedAt = link.CreatedAt };
    }

    // Idempotent: returns the active link, creating one only when there is none.
    [HttpPost("school-report-link")]
    public async Task<ActionResult<ReportLinkDto>> CreateSchoolLink()
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var link = await ActiveLinkAsync(me.SchoolId.Value);
        if (link is null)
        {
            link = new SchoolReportLink
            {
                SchoolId = me.SchoolId.Value,
                CreatedById = me.UserId,
                Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                    .Replace('+', '-').Replace('/', '_').TrimEnd('=')
            };
            db.SchoolReportLinks.Add(link);
            await db.SaveChangesAsync();
        }
        return new ReportLinkDto { Token = link.Token, CreatedAt = link.CreatedAt };
    }

    // Revoking kills the old link at once; creating a new one gives a fresh token.
    [HttpDelete("school-report-link")]
    public async Task<IActionResult> RevokeSchoolLink()
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        await db.SchoolReportLinks
            .Where(l => l.SchoolId == me.SchoolId && l.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.RevokedAt, DateTime.UtcNow));
        return NoContent();
    }

    // ── Telegram: the director gets the weekly report in the bot (no login) ──
    private static readonly TimeSpan TelegramCodeLifetime = TimeSpan.FromDays(7);
    private const int MaxTelegramConnections = 10;

    [HttpGet("telegram")]
    public async Task<ActionResult<TelegramStatusDto>> TelegramStatus()
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var o = telegram.Value;
        var since = DateTime.UtcNow - TelegramCodeLifetime;
        var rows = await db.TelegramConnections.AsNoTracking()
            .Where(c => c.SchoolId == me.SchoolId && (c.UserId == null || c.UserId == me.UserId) && (c.ChatId != null || c.CreatedAt > since))
            .OrderBy(c => c.CreatedAt).ToListAsync();

        return new TelegramStatusDto
        {
            Enabled = o.Enabled,
            BotUsername = o.BotUsername,
            Connections = rows.Select(c => new TelegramConnectionDto
            {
                Id = c.TelegramConnectionId,
                Connected = c.ChatId != null,
                IsMine = c.UserId != null,
                Name = c.TelegramName,
                CreatedAt = c.CreatedAt,
                ConnectedAt = c.ConnectedAt,
                Url = c.ChatId == null ? $"https://t.me/{o.BotUsername}?start={c.Code}" : null
            }).ToList()
        };
    }

    // A new one-time connection link for a director (or anybody who should get the weekly report).
    [HttpPost("telegram/link")]
    public async Task<ActionResult<TelegramConnectionDto>> CreateTelegramLink([FromQuery] bool mine = false)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });
        var o = telegram.Value;
        if (!o.Enabled) return Conflict(new { error = "TELEGRAM_OFF" });

        var since = DateTime.UtcNow - TelegramCodeLifetime;
        await db.TelegramConnections.Where(c => c.ChatId == null && c.CreatedAt <= since).ExecuteDeleteAsync();   // expired links
        // Recipients are capped per school, personal links per person.
        var count = mine
            ? await db.TelegramConnections.CountAsync(c => c.UserId == me.UserId)
            : await db.TelegramConnections.CountAsync(c => c.SchoolId == me.SchoolId && c.UserId == null);
        if (count >= (mine ? 3 : MaxTelegramConnections)) return Conflict(new { error = "TOO_MANY" });

        var c = new TelegramConnection
        {
            SchoolId = me.SchoolId.Value,
            CreatedById = me.UserId,
            UserId = mine ? me.UserId : null,
            Code = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=')
        };
        db.TelegramConnections.Add(c);
        await db.SaveChangesAsync();
        return new TelegramConnectionDto
        {
            Id = c.TelegramConnectionId,
            IsMine = mine,
            CreatedAt = c.CreatedAt,
            Url = $"https://t.me/{o.BotUsername}?start={c.Code}"
        };
    }

    // Disconnects a chat or cancels an unused link.
    [HttpDelete("telegram/{id:int}")]
    public async Task<IActionResult> RemoveTelegram(int id)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return Conflict(new { error = "NO_SCHOOL" });

        var n = await db.TelegramConnections
            .Where(c => c.TelegramConnectionId == id && c.SchoolId == me.SchoolId && (c.UserId == null || c.UserId == me.UserId))
            .ExecuteDeleteAsync();
        return n == 0 ? NotFound() : NoContent();
    }

    // Confirms a learner who picked this team: from now on they count in the reports.
    [HttpPost("classes/{id:int}/students/{userId:int}/approve")]
    public async Task<IActionResult> Approve(int id, int userId)
    {
        var cls = await FindOwnedAsync(id);
        if (cls is null) return NotFound();

        var student = await db.Users.SingleOrDefaultAsync(u => u.UserId == userId && u.ClassId == id && u.Role == UserRole.Learner);
        if (student is null) return NotFound();

        student.ClassApproved = true;
        await db.SaveChangesAsync();
        await telegramNotifier.NotifyPupilDecisionAsync(student.UserId, "ok", cls.Name);
        return NoContent();
    }

    [HttpPost("classes/{id:int}/approve-all")]
    public async Task<ActionResult<int>> ApproveAll(int id)
    {
        var cls = await FindOwnedAsync(id);
        if (cls is null) return NotFound();

        var waiting = await db.Users.AsNoTracking()
            .Where(u => u.ClassId == id && u.Role == UserRole.Learner && !u.ClassApproved).Select(u => u.UserId).ToListAsync();
        var count = await db.Users
            .Where(u => u.ClassId == id && u.Role == UserRole.Learner && !u.ClassApproved)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.ClassApproved, true));
        foreach (var pupilId in waiting) await telegramNotifier.NotifyPupilDecisionAsync(pupilId, "ok", cls.Name);
        return count;
    }

    // Rejecting a pending learner and removing an approved one are the same: the team and school text
    // disappear from the learner's profile (the region stays), so they pick again or stay unlinked.
    [HttpDelete("classes/{id:int}/students/{userId:int}")]
    public async Task<IActionResult> RemoveStudent(int id, int userId)
    {
        var cls = await FindOwnedAsync(id);
        if (cls is null) return NotFound();

        var student = await db.Users.SingleOrDefaultAsync(u => u.UserId == userId && u.ClassId == id);
        if (student is null) return NotFound();

        var wasApproved = student.ClassApproved;
        ClassesController.ClearTeam(student);
        await db.SaveChangesAsync();
        await telegramNotifier.NotifyPupilDecisionAsync(student.UserId, wasApproved ? "removed" : "no", cls.Name);
        return NoContent();
    }

    private async Task<User> GetMeAsync()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
    }

    private IQueryable<SchoolClass> VisibleClasses(User me) =>
        me.Role == UserRole.Director
            ? db.SchoolClasses.AsNoTracking().Where(c => c.SchoolId == me.SchoolId)
            : db.SchoolClasses.AsNoTracking().Where(c => c.TeacherId == me.UserId);

    private async Task<SchoolClass?> FindOwnedAsync(int id, bool track = false)
    {
        var me = await GetMeAsync();
        if (me.SchoolId is null) return null;

        var q = me.Role == UserRole.Director
            ? db.SchoolClasses.Where(c => c.SchoolId == me.SchoolId)
            : db.SchoolClasses.Where(c => c.TeacherId == me.UserId);
        if (!track) q = q.AsNoTracking();
        return await q.SingleOrDefaultAsync(c => c.ClassId == id);
    }

    private Task<SchoolReportLink?> ActiveLinkAsync(int schoolId) =>
        db.SchoolReportLinks.AsNoTracking().Where(l => l.SchoolId == schoolId && l.RevokedAt == null)
            .OrderByDescending(l => l.CreatedAt).FirstOrDefaultAsync();

    private async Task<bool> NameTakenAsync(int schoolId, string name, int? exceptId)
    {
        var lower = name.ToLower();
        return await db.SchoolClasses.AsNoTracking().AnyAsync(c =>
            c.SchoolId == schoolId && !c.IsArchived && c.Name.ToLower() == lower && c.ClassId != exceptId);
    }
}
