using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jumbo.Api.Services.Telegram;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Controllers;

// Telegram calls this for every message to the bot (setWebhook, see TelegramBackgroundService). It is anonymous by
// nature, so every call must carry the secret Telegram was given at registration; anything else is rejected.
[ApiController]
[Route("api/telegram")]
public class TelegramController(IOptions<TelegramOptions> options, TelegramBotService bot, ILogger<TelegramController> log) : ControllerBase
{
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook([FromBody] JsonElement update)
    {
        var o = options.Value;
        if (!o.Enabled) return NotFound();

        var given = Encoding.UTF8.GetBytes(Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString());
        var expected = Encoding.UTF8.GetBytes(o.WebhookSecret);
        if (given.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(given, expected))
            return Unauthorized();

        // Telegram retries on errors, so never fail the request because of one bad update.
        try { await bot.HandleUpdateAsync(update); }
        catch (Exception ex) { log.LogWarning(ex, "Telegram update failed"); }
        return Ok();
    }
}
