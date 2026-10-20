using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// One-time code that opens the website already signed in (the mobile app's "open on the site" and the bot's /play).
// The token itself never travels in a URL: the code works once, for a minute (see AuthController.RedeemWebLink).
public class WebLinkService(AppDbContext db)
{
    public const string Status = "WebLink";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(1);

    public async Task<QrLoginSession> CreateAsync(User user)
    {
        var now = DateTime.UtcNow;
        var session = new QrLoginSession
        {
            SessionId = Guid.NewGuid(),
            Status = Status,
            UserId = user.UserId,
            AccessToken = user.AccessToken,
            CreatedAt = now,
            ExpiresAt = now.Add(Lifetime)
        };

        // Old unused codes are not kept around.
        var stale = await db.QrLoginSessions.Where(s => s.Status == Status && s.ExpiresAt < now).ToListAsync();
        db.QrLoginSessions.RemoveRange(stale);
        db.QrLoginSessions.Add(session);
        await db.SaveChangesAsync();
        return session;
    }
}
