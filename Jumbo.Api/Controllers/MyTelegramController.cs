using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services.Telegram;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Controllers;

// A pupil connects their own Telegram to the bot from the app / site settings: one-time link, the first chat that
// opens it becomes theirs. Staff use the teacher cabinet instead (TeacherController).
[ApiController]
[Route("api/users/me/telegram")]
[RequireAccessToken]
public class MyTelegramController(AppDbContext db, ICurrentUserService currentUser, IOptions<TelegramOptions> options) : ControllerBase
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromDays(7);
    private const int MaxChats = 2;

    [HttpGet]
    public async Task<ActionResult<TelegramStatusDto>> Status()
    {
        var me = await GetLearnerAsync();
        if (me is null) return StatusCode(StatusCodes.Status403Forbidden, new { error = "LEARNERS_ONLY" });
        var o = options.Value;
        var since = DateTime.UtcNow - CodeLifetime;
        var rows = await db.TelegramConnections.AsNoTracking()
            .Where(c => c.UserId == me.UserId && (c.ChatId != null || c.CreatedAt > since))
            .OrderBy(c => c.CreatedAt).ToListAsync();
        return new TelegramStatusDto
        {
            Enabled = o.Enabled,
            BotUsername = o.BotUsername,
            Connections = rows.Select(c => ToDto(c, o)).ToList()
        };
    }

    [HttpPost("link")]
    public async Task<ActionResult<TelegramConnectionDto>> CreateLink()
    {
        var me = await GetLearnerAsync();
        if (me is null) return StatusCode(StatusCodes.Status403Forbidden, new { error = "LEARNERS_ONLY" });
        var o = options.Value;
        if (!o.Enabled) return Conflict(new { error = "TELEGRAM_OFF" });

        var since = DateTime.UtcNow - CodeLifetime;
        await db.TelegramConnections.Where(c => c.UserId == me.UserId && c.ChatId == null && c.CreatedAt <= since).ExecuteDeleteAsync();
        // An unused link is reused instead of piling up.
        var pending = await db.TelegramConnections.FirstOrDefaultAsync(c => c.UserId == me.UserId && c.ChatId == null);
        if (pending is not null) return ToDto(pending, o);

        if (await db.TelegramConnections.CountAsync(c => c.UserId == me.UserId) >= MaxChats) return Conflict(new { error = "TOO_MANY" });

        var c = new TelegramConnection
        {
            UserId = me.UserId,
            CreatedById = me.UserId,
            Code = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=')
        };
        db.TelegramConnections.Add(c);
        await db.SaveChangesAsync();
        return ToDto(c, o);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remove(int id)
    {
        var me = await GetLearnerAsync();
        if (me is null) return StatusCode(StatusCodes.Status403Forbidden, new { error = "LEARNERS_ONLY" });
        var n = await db.TelegramConnections.Where(c => c.TelegramConnectionId == id && c.UserId == me.UserId).ExecuteDeleteAsync();
        return n == 0 ? NotFound() : NoContent();
    }

    private static TelegramConnectionDto ToDto(TelegramConnection c, TelegramOptions o) => new()
    {
        Id = c.TelegramConnectionId,
        Connected = c.ChatId != null,
        IsMine = true,
        Name = c.TelegramName,
        CreatedAt = c.CreatedAt,
        ConnectedAt = c.ConnectedAt,
        Url = c.ChatId == null ? $"https://t.me/{o.BotUsername}?start={c.Code}" : null
    };

    private async Task<User?> GetLearnerAsync()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        return user.Role == UserRole.Learner ? user : null;
    }
}
