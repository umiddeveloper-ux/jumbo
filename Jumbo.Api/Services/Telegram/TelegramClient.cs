using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Services.Telegram;

public class TelegramOptions
{
    // Bot token from @BotFather. Comes from the server's environment (Telegram__BotToken), never from the repo.
    public string BotToken { get; set; } = string.Empty;

    public string BotUsername { get; set; } = "JumboUzBot";

    // Public address of the site: the bot's links and the webhook URL are built from it.
    public string SiteUrl { get; set; } = "https://jumbo.uz";

    // Local testing: log the messages instead of calling Telegram.
    public bool DryRun { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken);

    // Telegram echoes this in X-Telegram-Bot-Api-Secret-Token on every webhook call; derived from the token, so there
    // is no second secret to configure. Anyone without the token cannot forge an update.
    public string WebhookSecret
    {
        get
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(BotToken));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes("jumbo-telegram-webhook"))).ToLowerInvariant();
        }
    }
}

public enum SendResult { Sent, Blocked, Failed }

// Thin wrapper over the Telegram Bot HTTP API (sendMessage, setWebhook, answerCallbackQuery).
public class TelegramClient(HttpClient http, IOptions<TelegramOptions> options, ILogger<TelegramClient> log)
{
    private readonly TelegramOptions _o = options.Value;

    public async Task<SendResult> SendAsync(long chatId, string html, object? replyMarkup = null)
    {
        if (!_o.Enabled) return SendResult.Failed;
        if (_o.DryRun)
        {
            log.LogInformation("[telegram dry-run] to {Chat}: {Text}", chatId, html);
            if (replyMarkup is not null) log.LogInformation("[telegram dry-run] markup: {Markup}", JsonSerializer.Serialize(replyMarkup));
            return SendResult.Sent;
        }

        var payload = new Dictionary<string, object?>
        {
            ["chat_id"] = chatId,
            ["text"] = html,
            ["parse_mode"] = "HTML",
            ["link_preview_options"] = new { is_disabled = true }
        };
        if (replyMarkup is not null) payload["reply_markup"] = replyMarkup;

        var response = await PostAsync("sendMessage", payload);
        if (response is null) return SendResult.Failed;
        if (response.StatusCode == HttpStatusCode.OK) return SendResult.Sent;
        // 403: the user blocked the bot or deleted the chat: the connection is dead.
        return response.StatusCode == HttpStatusCode.Forbidden ? SendResult.Blocked : SendResult.Failed;
    }

    // The small toast (or alert box) that answers a button press; every callback must be answered once.
    public async Task AnswerCallbackAsync(string callbackId, string? text = null, bool alert = false)
    {
        if (!_o.Enabled) return;
        if (_o.DryRun)
        {
            if (text is not null) log.LogInformation("[telegram dry-run] callback answer: {Text}", text);
            return;
        }
        await PostAsync("answerCallbackQuery", new Dictionary<string, object?> { ["callback_query_id"] = callbackId, ["text"] = text, ["show_alert"] = alert });
    }

    // Replaces the text of a message the bot sent (and drops its buttons): used to show the result of a decision.
    public async Task EditAsync(long chatId, long messageId, string html)
    {
        if (!_o.Enabled) return;
        if (_o.DryRun)
        {
            log.LogInformation("[telegram dry-run] edit {Chat}/{Msg}: {Text}", chatId, messageId, html);
            return;
        }
        await PostAsync("editMessageText", new Dictionary<string, object?>
        {
            ["chat_id"] = chatId, ["message_id"] = messageId, ["text"] = html, ["parse_mode"] = "HTML",
            ["link_preview_options"] = new { is_disabled = true }
        });
    }

    public async Task<bool> SetWebhookAsync(string url)
    {
        if (!_o.Enabled) return false;
        var response = await PostAsync("setWebhook", new Dictionary<string, object?>
        {
            ["url"] = url,
            ["secret_token"] = _o.WebhookSecret,
            ["allowed_updates"] = new[] { "message", "callback_query" },
            ["drop_pending_updates"] = false
        });
        return response?.StatusCode == HttpStatusCode.OK;
    }

    // Generic call for the profile settings (setMyName, setMyDescription, setMyCommands ...).
    public async Task<bool> CallAsync(string method, Dictionary<string, object?> payload)
    {
        if (!_o.Enabled || _o.DryRun) return false;
        var response = await PostAsync(method, payload);
        return response?.StatusCode == HttpStatusCode.OK;
    }

    // The command menu of ONE chat (scope "chat"): it overrides the default menu for that person only.
    public async Task<bool> SetChatCommandsAsync(long chatId, IEnumerable<(string Command, string Text)> commands)
    {
        if (!_o.Enabled) return false;
        if (_o.DryRun)
        {
            log.LogInformation("[telegram dry-run] menu of {Chat}: {Cmds}", chatId, string.Join(" ", commands.Select(c => "/" + c.Command)));
            return true;
        }
        return await CallAsync("setMyCommands", new Dictionary<string, object?>
        {
            ["commands"] = commands.Select(c => new { command = c.Command, description = c.Text }).ToArray(),
            ["scope"] = new { type = "chat", chat_id = chatId }
        });
    }

    // Back to the default menu (a person who disconnected).
    public async Task DeleteChatCommandsAsync(long chatId)
    {
        if (!_o.Enabled) return;
        if (_o.DryRun) { log.LogInformation("[telegram dry-run] menu of {Chat}: default", chatId); return; }
        await CallAsync("deleteMyCommands", new Dictionary<string, object?> { ["scope"] = new { type = "chat", chat_id = chatId } });
    }

    // getMyName / getMyDescription / getMyShortDescription: returns the current value for a language ("" = default).
    public async Task<string?> GetProfileValueAsync(string method, string field, string language)
    {
        if (!_o.Enabled || _o.DryRun) return null;
        var response = await PostAsync(method, new Dictionary<string, object?> { ["language_code"] = language });
        if (response is null || !response.IsSuccessStatusCode) return null;
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("result", out var r) && r.TryGetProperty(field, out var v) ? v.GetString() : null;
    }

    private async Task<HttpResponseMessage?> PostAsync(string method, object body)
    {
        try
        {
            // Leading slash: the token contains a colon, and "bot123:abc/..." would be read as a URI scheme.
            var response = await http.PostAsync($"/bot{_o.BotToken}/{method}",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"));
            if (!response.IsSuccessStatusCode)
                // The body is logged, the URL (it contains the token) never is.
                log.LogWarning("Telegram {Method} failed: {Status} {Body}", method, (int)response.StatusCode, await response.Content.ReadAsStringAsync());
            return response;
        }
        catch (Exception ex)
        {
            log.LogWarning("Telegram {Method} error: {Type}", method, ex.GetType().Name);
            return null;
        }
    }
}
