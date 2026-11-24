using System.Security.Cryptography;
using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// A teacher's reset of a pupil's forgotten password (the cabinet and the Telegram bot do the same thing): a random 6-digit password is set,
// the pupil is signed out of every device (the access token changes) and the teacher is told the new password once - it is not stored anywhere.
// Only an approved pupil of the team the caller has already been checked to own.
public class PupilPasswordService(AppDbContext db)
{
    public record Reset(string Password, string Name, string? Username, string ClassName);

    public async Task<Reset?> ResetAsync(SchoolClass cls, int pupilId)
    {
        var pupil = await db.Users.SingleOrDefaultAsync(u =>
            u.UserId == pupilId && u.ClassId == cls.ClassId && u.Role == UserRole.Learner && u.ClassApproved);
        if (pupil is null) return null;

        var password = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        pupil.PasswordHash = new PasswordHasher<User>().HashPassword(pupil, password);
        pupil.AccessToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        await db.SaveChangesAsync();
        return new Reset(password, $"{pupil.FirstName} {pupil.LastName}".Trim(), pupil.Username, cls.Name);
    }
}
