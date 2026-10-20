using System.Net.Mail;
using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>The footer form of the website: feedback (a message) and "subscribe" (news). Open to everybody; an address, a language and the message are kept.</summary>
[ApiController]
[Route("api/subscribe")]
public class SubscribeController(AppDbContext db) : ControllerBase
{
    // The website calls from one server, so the visitor's own address is not seen here: the guard is a cap for the whole service
    // (a flood of fake addresses is stopped), one address is kept once.
    private static readonly object Gate = new();
    private static DateTime _windowStart = DateTime.UtcNow;
    private static int _inWindow;
    private const int MaxPerHour = 120;

    [HttpPost]
    public async Task<IActionResult> Subscribe(SubscribeRequest request)
    {
        var email = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        if (!IsValid(email)) return BadRequest(new { error = "INVALID_EMAIL" });

        lock (Gate)
        {
            if (DateTime.UtcNow - _windowStart > TimeSpan.FromHours(1)) { _windowStart = DateTime.UtcNow; _inWindow = 0; }
            if (++_inWindow > MaxPerHour) return StatusCode(429, new { error = "TOO_MANY" });
        }

        var lang = request.Lang is "ru" or "en" or "uz" ? request.Lang : "ru";
        var message = (request.Message ?? string.Empty).Trim();
        if (message.Length > 1000) message = message[..1000];
        bool news = request.Subscribe ?? true;
        if (message.Length == 0 && !news) return BadRequest(new { error = "NOTHING_TO_SAVE" });

        // the message is kept as it is (every message, also from an address that wrote before)
        if (message.Length > 0)
            db.FeedbackMessages.Add(new FeedbackMessage { Email = email, Message = message, Lang = lang });
        await db.SaveChangesAsync();

        // an address that is already subscribed is not an error (and does not tell a stranger who is subscribed)
        if (news && !await db.Subscribers.AnyAsync(x => x.Email == email))
        {
            db.Subscribers.Add(new Subscriber { Email = email, Lang = lang });
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateException) { /* the same address came twice at once */ }
        }
        return Ok(new { status = "ok" });
    }

    private static bool IsValid(string email)
    {
        if (email.Length is < 6 or > 200) return false;
        try
        {
            var a = new MailAddress(email);
            return a.Address == email && email.IndexOf('@') > 0 && email.LastIndexOf('.') > email.IndexOf('@') + 1 && email[^1] != '.';
        }
        catch (FormatException) { return false; }
    }
}
