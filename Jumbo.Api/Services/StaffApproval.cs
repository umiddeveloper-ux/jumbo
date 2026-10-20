using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// What approving / rejecting a staff request does. Used by the admin page and by a director's button in the bot,
// so both give exactly the same result.
public static class StaffApproval
{
    /// <summary>
    /// A teacher joins at once, nobody confirms (the admin only watches the list). The school is the one with this name in the region, or a new
    /// one: it is marked verified, because the learners pick their school from the verified ones and the teacher's team has to be found.
    /// (A director is different: he sees the whole school, so his request still waits for the admin.)
    /// </summary>
    public static async Task ApproveTeacherAsync(AppDbContext db, StaffRequest req, User user)
    {
        var name = req.SchoolName.Trim();
        var lower = name.ToLower();
        var school = await db.Schools.FirstOrDefaultAsync(s => s.Region == req.Region && s.Name.ToLower() == lower);
        if (school is null)
        {
            school = new School { Region = req.Region, Name = name.Length > 150 ? name[..150] : name, IsVerified = true };
            db.Schools.Add(school);
            await db.SaveChangesAsync();
        }
        Approve(req, user, school, null, null);
        await db.SaveChangesAsync();
    }

    public static void Approve(StaffRequest req, User user, School school, int? reviewerId, string? note)
    {
        if (user.Role != UserRole.Admin)
            user.Role = req.RequestedRole;
        user.SchoolId = school.SchoolId;
        user.ClassId = null;   // staff are not learners of any team
        user.ClassApproved = false;
        // Keep the learner-facing profile consistent (exam gate, ratings).
        user.School = school.Name;
        if (string.IsNullOrWhiteSpace(user.Region)) user.Region = school.Region;

        req.Status = StaffRequestStatus.Approved;
        req.SchoolId = school.SchoolId;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = reviewerId;
        req.AdminNote = note;
    }

    public static void Reject(StaffRequest req, int reviewerId, string? note)
    {
        req.Status = StaffRequestStatus.Rejected;
        req.ReviewedAt = DateTime.UtcNow;
        req.ReviewedByUserId = reviewerId;
        req.AdminNote = note;
    }
}
