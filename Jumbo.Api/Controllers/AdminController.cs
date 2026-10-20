using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

// Platform administration: decide staff requests and keep the school directory. Admin role only.
[ApiController]
[Route("api/admin")]
[RequireRole(UserRole.Admin)]
public class AdminController(AppDbContext db, ICurrentUserService currentUser, SeedDataService seedData) : ControllerBase
{
    // ── versions of the apps: what the apps are told at their start ───
    [HttpGet("app-versions")]
    public async Task<List<AppVersionDto>> GetAppVersions()
    {
        var rows = await db.AppVersions.AsNoTracking().ToListAsync();
        return AppVersionController.Platforms.Select(p => rows.FirstOrDefault(r => r.Platform == p) is { } r
            ? AppVersionController.ToDto(r) : new AppVersionDto { Platform = p }).ToList();
    }

    [HttpPut("app-versions/{platform}")]
    public async Task<IActionResult> SetAppVersion(string platform, AppVersionDto request)
    {
        platform = platform.ToLowerInvariant();
        if (!AppVersionController.Platforms.Contains(platform)) return NotFound();
        if (request.LatestCode < 0 || request.MinCode < 0 || request.MinCode > request.LatestCode && request.LatestCode > 0)
            return BadRequest(new { error = "MIN_ABOVE_LATEST" });
        var url = request.Url?.Trim();
        if (!string.IsNullOrEmpty(url) && !(url.StartsWith("https://") || url.StartsWith("market://") || url.StartsWith("itms-apps://") || url.StartsWith("/")))
            return BadRequest(new { error = "BAD_URL" });

        string? Cut(string? v) { v = v?.Trim(); return string.IsNullOrEmpty(v) ? null : (v.Length > 500 ? v[..500] : v); }
        var row = await db.AppVersions.SingleOrDefaultAsync(x => x.Platform == platform);
        if (row is null) { row = new AppVersion { Platform = platform }; db.AppVersions.Add(row); }
        row.LatestCode = request.LatestCode;
        row.MinCode = request.MinCode;
        row.Url = string.IsNullOrEmpty(url) ? null : url;
        row.NotesRu = Cut(request.NotesRu);
        row.NotesEn = Cut(request.NotesEn);
        row.NotesUz = Cut(request.NotesUz);
        row.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ── versions of the app (bronze / silver / gold): given by hand until the payment is built ───
    [HttpGet("plans")]
    public async Task<List<PlanUserDto>> GetPlanUsers()
    {
        var now = DateTime.UtcNow;
        var rows = await db.Users.AsNoTracking().Where(u => u.Plan != Jumbo.Shared.Plans.Bronze)
            .OrderBy(u => u.PlanUntil).Take(500).ToListAsync();
        return rows.Select(u => new PlanUserDto
        {
            UserId = u.UserId, PublicId = u.PublicId, Name = (u.FirstName + " " + u.LastName).Trim(), Username = u.Username,
            Plan = Jumbo.Shared.Plans.Effective(u.Plan, u.PlanUntil, now), PlanUntil = u.PlanUntil
        }).ToList();
    }

    [HttpPut("plans")]
    public async Task<ActionResult<PlanUserDto>> SetPlan(SetPlanRequest request)
    {
        var login = request.Username?.Trim().ToLower();
        if (string.IsNullOrEmpty(login)) return BadRequest(new { error = "NO_USER" });
        var publicId = login.ToUpperInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => (u.Username != null && u.Username.ToLower() == login) || u.PublicId == publicId);
        if (user is null) return NotFound(new { error = "NO_USER" });

        var plan = Jumbo.Shared.Plans.Normalize(request.Plan);
        if (plan == Jumbo.Shared.Plans.Bronze) { user.Plan = plan; user.PlanUntil = null; }
        else
        {
            if (request.Until is not { } until || until.ToUniversalTime() <= DateTime.UtcNow) return BadRequest(new { error = "BAD_DATE" });
            user.Plan = plan;
            user.PlanUntil = DateTime.SpecifyKind(until.ToUniversalTime(), DateTimeKind.Utc);
        }
        await db.SaveChangesAsync();
        return new PlanUserDto { UserId = user.UserId, PublicId = user.PublicId, Name = (user.FirstName + " " + user.LastName).Trim(), Username = user.Username, Plan = user.Plan, PlanUntil = user.PlanUntil };
    }

    // ── the prices of the versions (four numbers) and the balance of the learners ───
    [HttpGet("plan-prices")]
    public Task<List<PlanPriceDto>> GetPlanPrices() => PlansController.AllPricesAsync(db);

    [HttpPut("plan-prices")]
    public async Task<ActionResult<List<PlanPriceDto>>> SetPlanPrices(SetPlanPricesRequest request)
    {
        foreach (var p in request.Prices ?? [])
        {
            var plan = Jumbo.Shared.Plans.Normalize(p.Plan);
            if (!PlansController.PaidPlans.Contains(plan) || !Jumbo.Shared.Plans.Periods.Contains(p.Period ?? string.Empty)) return BadRequest(new { error = "BAD_PRICE" });
            if (p.Amount is < 0 or > 10_000_000_000) return BadRequest(new { error = "BAD_PRICE" });

            var row = await db.PlanPrices.SingleOrDefaultAsync(x => x.Plan == plan && x.Period == p.Period);
            // nothing or zero = not for sale yet
            if (p.Amount is null or 0) { if (row is not null) db.PlanPrices.Remove(row); continue; }
            if (row is null) { row = new PlanPrice { Plan = plan, Period = p.Period! }; db.PlanPrices.Add(row); }
            row.Amount = p.Amount.Value;
            row.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        return await PlansController.AllPricesAsync(db);
    }

    [HttpPost("balance/topup")]
    public async Task<ActionResult<PlanUserDto>> TopUp(TopUpRequest request)
    {
        if (request.Amount is < 1 or > 1_000_000_000) return BadRequest(new { error = "BAD_AMOUNT" });
        var login = request.Username?.Trim().ToLower();
        if (string.IsNullOrEmpty(login)) return BadRequest(new { error = "NO_USER" });
        var publicId = login.ToUpperInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => (u.Username != null && u.Username.ToLower() == login) || u.PublicId == publicId);
        if (user is null) return NotFound(new { error = "NO_USER" });

        var (_, balance) = await Jumbo.Api.Services.BalanceService.TopUpAsync(db, user.UserId, request.Amount, "admin", null, request.Note);
        return new PlanUserDto { UserId = user.UserId, PublicId = user.PublicId, Name = (user.FirstName + " " + user.LastName).Trim(), Username = user.Username, Plan = user.Plan, PlanUntil = user.PlanUntil, Balance = balance };
    }

    [HttpGet("balance/transactions")]
    public async Task<List<BalanceTransactionDto>> BalanceTransactions() =>
        await (from t in db.BalanceTransactions.AsNoTracking()
               join u in db.Users.AsNoTracking() on t.UserId equals u.UserId
               orderby t.BalanceTransactionId descending
               select new BalanceTransactionDto
               {
                   CreatedAt = t.CreatedAt, Kind = t.Kind, Provider = t.Provider, Amount = t.Amount, BalanceAfter = t.BalanceAfter, Note = t.Note,
                   PublicId = u.PublicId, UserName = (u.FirstName + " " + u.LastName).Trim()
               }).Take(100).ToListAsync();

    // ── subscribers of the website footer ───
    [HttpGet("subscribers")]
    public async Task<List<SubscriberDto>> GetSubscribers() =>
        await db.Subscribers.AsNoTracking().OrderByDescending(x => x.CreatedAt)
            .Select(x => new SubscriberDto { Email = x.Email, Lang = x.Lang, CreatedAt = x.CreatedAt }).ToListAsync();

    [HttpGet("feedback")]
    public async Task<List<FeedbackDto>> GetFeedback() =>
        await db.FeedbackMessages.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(500)
            .Select(x => new FeedbackDto { Email = x.Email, Message = x.Message, Lang = x.Lang, CreatedAt = x.CreatedAt }).ToListAsync();

    // ── books ───────────────────────────────────────────────────────────────
    // Replaces one book with its SeedData file (new edition). Deletes the learners' progress in that book, so it needs
    // ?confirm=true on purpose.
    [HttpPost("books/{bookNumber:int}/reload")]
    public async Task<IActionResult> ReloadBook(int bookNumber, [FromQuery] bool confirm = false)
    {
        if (!confirm) return BadRequest(new { error = "CONFIRM_REQUIRED" });
        try
        {
            var (subjects, words) = await seedData.ReloadBookAsync(bookNumber);
            return Ok(new { bookNumber, subjects, words });
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // Removes one book from the database (topics, words, pictures, learners' progress in it). Needs ?confirm=true on purpose.
    [HttpDelete("books/{bookNumber:int}")]
    public async Task<IActionResult> RemoveBook(int bookNumber, [FromQuery] bool confirm = false)
    {
        if (!confirm) return BadRequest(new { error = "CONFIRM_REQUIRED" });
        var removed = await seedData.RemoveBooksAsync([bookNumber]);
        return removed == 0 ? NotFound(new { error = "BOOK_NOT_IN_DATABASE" }) : NoContent();
    }

    // ── staff requests ──────────────────────────────────────────────────────
    [HttpGet("staff-requests")]
    public async Task<ActionResult<List<StaffRequestDto>>> Requests([FromQuery] string status = "pending")
    {
        var q = db.StaffRequests.AsNoTracking().AsQueryable();
        q = status.ToLowerInvariant() switch
        {
            "pending" => q.Where(r => r.Status == StaffRequestStatus.Pending),
            "approved" => q.Where(r => r.Status == StaffRequestStatus.Approved),
            "rejected" => q.Where(r => r.Status == StaffRequestStatus.Rejected),
            _ => q
        };

        var rows = await q.OrderBy(r => r.Status).ThenByDescending(r => r.CreatedAt).Take(200).ToListAsync();
        var userIds = rows.Select(r => r.UserId).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId))
            .Select(u => new { u.UserId, u.FirstName, u.LastName, u.Username })
            .ToDictionaryAsync(u => u.UserId);

        return rows.Select(r =>
        {
            users.TryGetValue(r.UserId, out var u);
            return ToDto(r, u is null ? null : ($"{u.FirstName} {u.LastName}".Trim(), u.Username));
        }).ToList();
    }

    [HttpPost("staff-requests/{id:int}/approve")]
    public async Task<ActionResult<StaffRequestDto>> Approve(int id, ApproveStaffRequest body)
    {
        var req = await db.StaffRequests.SingleOrDefaultAsync(r => r.StaffRequestId == id);
        if (req is null) return NotFound();
        if (req.Status != StaffRequestStatus.Pending) return Conflict(new { error = "ALREADY_DECIDED" });

        School school;
        if (body.SchoolId is { } sid)
        {
            var existing = await db.Schools.SingleOrDefaultAsync(s => s.SchoolId == sid);
            if (existing is null) return BadRequest(new { error = "School not found." });
            school = existing;
        }
        else
        {
            var name = (body.NewSchoolName ?? req.SchoolName).Trim();
            if (name.Length < 2) return BadRequest(new { error = "School name is required." });
            // Reuse a school with the same name in the region instead of creating a duplicate.
            var lower = name.ToLower();
            school = await db.Schools.FirstOrDefaultAsync(s => s.Region == req.Region && s.Name.ToLower() == lower)
                     ?? new School { Region = req.Region, Name = name.Length > 150 ? name[..150] : name };
            school.IsVerified = true;
            if (school.SchoolId == 0) db.Schools.Add(school);
            await db.SaveChangesAsync();
        }

        var reviewerId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.SingleAsync(u => u.UserId == req.UserId);
        Jumbo.Api.Services.StaffApproval.Approve(req, user, school, reviewerId, Clean(body.Note));

        await db.SaveChangesAsync();
        return ToDto(req, ($"{user.FirstName} {user.LastName}".Trim(), user.Username));
    }

    [HttpPost("staff-requests/{id:int}/reject")]
    public async Task<ActionResult<StaffRequestDto>> Reject(int id, RejectStaffRequest body)
    {
        var req = await db.StaffRequests.SingleOrDefaultAsync(r => r.StaffRequestId == id);
        if (req is null) return NotFound();
        if (req.Status != StaffRequestStatus.Pending) return Conflict(new { error = "ALREADY_DECIDED" });

        Jumbo.Api.Services.StaffApproval.Reject(req, (await currentUser.GetUserIdAsync())!.Value, Clean(body.Note));
        await db.SaveChangesAsync();
        return ToDto(req, null);
    }

    // ── school directory ────────────────────────────────────────────────────
    [HttpGet("schools")]
    public async Task<ActionResult<List<SchoolDto>>> Schools([FromQuery] string? region = null)
    {
        var q = db.Schools.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(region)) q = q.Where(s => s.Region == region);

        var schools = await q.OrderBy(s => s.Region).ThenBy(s => s.Name).Take(500).ToListAsync();
        var staff = await db.Users.AsNoTracking()
            .Where(u => u.SchoolId != null && (u.Role == UserRole.Teacher || u.Role == UserRole.Director))
            .GroupBy(u => u.SchoolId!.Value)
            .Select(g => new { SchoolId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SchoolId, x => x.Count);

        return schools.Select(s => new SchoolDto
        {
            SchoolId = s.SchoolId, Region = s.Region, Name = s.Name, IsVerified = s.IsVerified,
            StaffCount = staff.GetValueOrDefault(s.SchoolId)
        }).ToList();
    }

    [HttpPost("schools")]
    public async Task<ActionResult<SchoolDto>> CreateSchool(CreateSchoolRequest body)
    {
        var name = body.Name?.Trim();
        if (!RegionCodes.IsValid(body.Region)) return BadRequest(new { error = "Unknown region." });
        if (string.IsNullOrEmpty(name) || name.Length < 2) return BadRequest(new { error = "School name is required." });

        var lower = name.ToLower();
        var dupe = await db.Schools.AnyAsync(s => s.Region == body.Region && s.Name.ToLower() == lower);
        if (dupe) return Conflict(new { error = "SCHOOL_EXISTS" });

        var school = new School { Region = body.Region, Name = name.Length > 150 ? name[..150] : name, IsVerified = true };
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        return new SchoolDto { SchoolId = school.SchoolId, Region = school.Region, Name = school.Name, IsVerified = true };
    }

    [HttpPut("schools/{id:int}/verified")]
    public async Task<IActionResult> SetVerified(int id, SetVerifiedRequest body)
    {
        var school = await db.Schools.SingleOrDefaultAsync(s => s.SchoolId == id);
        if (school is null) return NotFound();
        school.IsVerified = body.IsVerified;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private static string? Clean(string? v)
    {
        var t = v?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > 500 ? t[..500] : t;
    }

    internal static StaffRequestDto ToDto(StaffRequest r, (string Name, string? Login)? applicant) => new()
    {
        Id = r.StaffRequestId, Role = r.RequestedRole, Region = r.Region, SchoolName = r.SchoolName,
        Phone = r.Phone, Comment = r.Comment, Status = r.Status, CreatedAt = r.CreatedAt,
        ReviewedAt = r.ReviewedAt, AdminNote = r.AdminNote, SchoolId = r.SchoolId, UserId = r.UserId,
        ApplicantName = applicant?.Name, ApplicantLogin = applicant?.Login
    };
}
