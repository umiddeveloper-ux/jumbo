using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services.Telegram;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// The learner side of teams: see my team, pick one from the list, leave. The Teacher decides (TeacherController).
[ApiController]
[Route("api/classes")]
[RequireAccessToken]
public class ClassesController(AppDbContext db, ICurrentUserService currentUser, TelegramNotifier telegramNotifier) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<MyTeamDto>> Mine()
    {
        var user = await GetUserAsync(track: false);
        if (user.ClassId is null) return NotFound();

        var cls = await db.SchoolClasses.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == user.ClassId);
        if (cls is null) return NotFound();
        return await ToDtoAsync(db, cls, user.ClassApproved);
    }

    // Picking a team (or changing it) puts the learner in the teacher's pending list. Re-picking the team the
    // learner is already approved in changes nothing.
    [HttpPost("select")]
    public async Task<ActionResult<MyTeamDto>> Select(SelectTeamRequest request)
    {
        var user = await GetUserAsync(track: true);
        if (user.Role != UserRole.Learner)
            return Conflict(new { error = "STAFF_CANNOT_JOIN" });

        var cls = await db.SchoolClasses.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == request.ClassId);
        if (cls is null) return NotFound(new { error = "TEAM_NOT_FOUND" });
        if (cls.IsArchived) return Conflict(new { error = "TEAM_ARCHIVED" });

        var keepApproval = user.ClassId == cls.ClassId && user.ClassApproved;
        await ApplyTeamAsync(db, user, cls);
        user.ClassApproved = keepApproval;
        await db.SaveChangesAsync();
        if (!keepApproval) await telegramNotifier.NotifyPendingAsync(cls.ClassId, user.UserId);
        return await ToDtoAsync(db, cls, user.ClassApproved);
    }

    // The school / team text goes away with the membership; the region stays.
    [HttpPost("leave")]
    public async Task<IActionResult> Leave()
    {
        var user = await GetUserAsync(track: true);
        ClearTeam(user);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // Shared with registration (UsersController) and the teacher's "reject".
    internal static async Task ApplyTeamAsync(AppDbContext db, User user, SchoolClass cls)
    {
        var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == cls.SchoolId);
        user.ClassId = cls.ClassId;
        user.ClassApproved = false;
        user.Region = school.Region;
        user.School = school.Name;
        user.Team = cls.Name;
    }

    internal static void ClearTeam(User user)
    {
        user.ClassId = null;
        user.ClassApproved = false;
        user.School = null;
        user.Team = null;
    }

    private async Task<User> GetUserAsync(bool track)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var q = track ? db.Users.AsQueryable() : db.Users.AsNoTracking();
        return await q.SingleAsync(u => u.UserId == userId);
    }

    private static async Task<MyTeamDto> ToDtoAsync(AppDbContext db, SchoolClass cls, bool approved)
    {
        var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == cls.SchoolId);
        var teacher = await db.Users.AsNoTracking()
            .Where(u => u.UserId == cls.TeacherId)
            .Select(u => new { u.FirstName, u.LastName })
            .SingleOrDefaultAsync();
        return new MyTeamDto
        {
            ClassId = cls.ClassId,
            ClassName = cls.Name,
            SchoolId = school.SchoolId,
            SchoolName = school.Name,
            Region = school.Region,
            TeacherName = teacher is null ? null : $"{teacher.FirstName} {teacher.LastName}".Trim(),
            Approved = approved
        };
    }
}
