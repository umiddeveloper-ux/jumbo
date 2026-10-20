using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// "For schools": a signed-in user asks to become a Teacher or Director. Nothing is granted here;
// an Admin decides (see AdminController).
[ApiController]
[Route("api/staff-requests")]
[RequireAccessToken]
public class StaffRequestsController(AppDbContext db, ICurrentUserService currentUser, Jumbo.Api.Services.Telegram.TelegramNotifier telegramNotifier) : ControllerBase
{
    // My latest request (any status), or 404 when I never applied.
    [HttpGet("mine")]
    public async Task<ActionResult<StaffRequestDto>> Mine()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var r = await db.StaffRequests.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync();
        if (r is null) return NotFound();

        // A teacher's request that was left waiting before the confirmation was dropped: it is approved now.
        if (r.Status == StaffRequestStatus.Pending && r.RequestedRole == UserRole.Teacher)
        {
            var applicant = await db.Users.SingleAsync(u => u.UserId == userId);
            if (applicant.Role is not (UserRole.Teacher or UserRole.Director or UserRole.Admin))
            {
                var tracked = await db.StaffRequests.SingleAsync(x => x.StaffRequestId == r.StaffRequestId);
                await Jumbo.Api.Services.StaffApproval.ApproveTeacherAsync(db, tracked, applicant);
                r = tracked;
            }
        }
        return AdminController.ToDto(r, null);
    }

    [HttpPost]
    public async Task<ActionResult<StaffRequestDto>> Create(CreateStaffRequest request)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;

        if (request.Role is not (UserRole.Teacher or UserRole.Director))
            return BadRequest(new { error = "Unknown role." });
        if (!RegionCodes.IsValid(request.Region))
            return BadRequest(new { error = "Unknown region." });

        var schoolName = request.SchoolName?.Trim();
        if (string.IsNullOrEmpty(schoolName) || schoolName.Length < 2)
            return BadRequest(new { error = "School name is required." });

        var me = await db.Users.SingleAsync(u => u.UserId == userId);
        if (me.Role is UserRole.Teacher or UserRole.Director or UserRole.Admin)
            return Conflict(new { error = "ALREADY_STAFF" });

        var hasPending = await db.StaffRequests.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.Status == StaffRequestStatus.Pending);
        if (hasPending) return Conflict(new { error = "PENDING_EXISTS" });

        var entity = new StaffRequest
        {
            UserId = userId,
            RequestedRole = request.Role,
            Region = request.Region,
            SchoolName = Trim(schoolName, 150)!,
            Phone = Jumbo.Shared.PhoneNumbers.Clean(request.Phone),
            Comment = Trim(request.Comment, 500),
            Status = StaffRequestStatus.Pending
        };
        db.StaffRequests.Add(entity);
        await db.SaveChangesAsync();

        // A teacher is connected at once; only a director waits for the admin (and the directors of the school are told about the request).
        if (entity.RequestedRole == UserRole.Teacher)
            await Jumbo.Api.Services.StaffApproval.ApproveTeacherAsync(db, entity, me);
        else
            await telegramNotifier.NotifyStaffRequestAsync(entity.StaffRequestId);
        return AdminController.ToDto(entity, null);
    }

    // Verified schools of a region: suggestions for the school field of the form.
    [HttpGet("/api/schools")]
    public async Task<ActionResult<List<SchoolDto>>> Schools([FromQuery] string region)
    {
        if (!RegionCodes.IsValid(region)) return new List<SchoolDto>();
        return await db.Schools.AsNoTracking()
            .Where(s => s.Region == region && s.IsVerified)
            .OrderBy(s => s.Name)
            .Select(s => new SchoolDto { SchoolId = s.SchoolId, Region = s.Region, Name = s.Name, IsVerified = true })
            .ToListAsync();
    }

    private static string? Trim(string? v, int max)
    {
        var t = v?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }
}
