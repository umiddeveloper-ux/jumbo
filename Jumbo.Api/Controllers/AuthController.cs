using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, ICurrentUserService currentUser, Jumbo.Api.Services.WebLinkService webLinks) : ControllerBase
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(2);

    [HttpPost("qr/create")]
    public async Task<ActionResult<QrSessionResponse>> CreateQrSession()
    {
        var now = DateTime.UtcNow;
        var session = new QrLoginSession
        {
            SessionId = Guid.NewGuid(),
            Status = "Pending",
            CreatedAt = now,
            ExpiresAt = now.Add(SessionLifetime)
        };

        db.QrLoginSessions.Add(session);
        await db.SaveChangesAsync();

        return new QrSessionResponse { SessionId = session.SessionId, ExpiresAt = session.ExpiresAt };
    }

    [HttpGet("qr/{sessionId:guid}/status")]
    public async Task<ActionResult<QrStatusResponse>> GetQrStatus(Guid sessionId)
    {
        var session = await db.QrLoginSessions.AsNoTracking().SingleOrDefaultAsync(s => s.SessionId == sessionId);
        if (session is null) return NotFound();

        if (session.Status == "Pending" && DateTime.UtcNow > session.ExpiresAt)
            return new QrStatusResponse { Status = "Expired" };

        return new QrStatusResponse
        {
            Status = session.Status,
            AccessToken = session.Status == "Confirmed" ? session.AccessToken : null
        };
    }

    [HttpPost("qr/{sessionId:guid}/confirm")]
    [RequireAccessToken]
    public async Task<IActionResult> ConfirmQrSession(Guid sessionId)
    {
        var session = await db.QrLoginSessions.SingleOrDefaultAsync(s => s.SessionId == sessionId);
        if (session is null) return NotFound();

        if (session.Status != "Pending" || DateTime.UtcNow > session.ExpiresAt)
            return BadRequest(new { error = "QR-код больше не действителен." });

        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);

        session.UserId = userId;
        session.AccessToken = user.AccessToken;
        session.Status = "Confirmed";

        await db.SaveChangesAsync();
        return NoContent();
    }

    // "Open on the website already signed in" (the mobile app): the app asks for a one-time code, the website
    // trades it for the access token. The token itself never travels in a URL; the code works once, for a minute.
    private static readonly TimeSpan WebLinkLifetime = TimeSpan.FromMinutes(1);
    private const string WebLinkStatus = "WebLink";

    [HttpPost("web-link")]
    [RequireAccessToken]
    public async Task<ActionResult<QrSessionResponse>> CreateWebLink()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);

        var session = await webLinks.CreateAsync(user);

        return new QrSessionResponse { SessionId = session.SessionId, ExpiresAt = session.ExpiresAt };
    }

    // Called by the website (server to server): returns the token once, then the code is spent.
    [HttpPost("web-link/{code:guid}/redeem")]
    public async Task<ActionResult<QrStatusResponse>> RedeemWebLink(Guid code)
    {
        var session = await db.QrLoginSessions.SingleOrDefaultAsync(s => s.SessionId == code && s.Status == WebLinkStatus);
        if (session is null || DateTime.UtcNow > session.ExpiresAt || string.IsNullOrEmpty(session.AccessToken))
            return NotFound();

        var token = session.AccessToken;
        session.Status = "Used";
        session.AccessToken = null;
        await db.SaveChangesAsync();
        return new QrStatusResponse { Status = "Confirmed", AccessToken = token };
    }
}
