using Jumbo.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Services.Telegram;

// Runs next to the API: registers the webhook once at start and sends the weekly digest.
// Digest time: Monday from 09:00 to 21:00 app time (UTC+5, Tashkent); a chat gets it once a week.
public class TelegramBackgroundService(IServiceScopeFactory scopes, TelegramClient telegram, IOptions<TelegramOptions> options, ILogger<TelegramBackgroundService> log) : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromMinutes(1);
    private readonly TelegramOptions _o = options.Value;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_o.Enabled)
        {
            log.LogInformation("Telegram bot is off: no Telegram:BotToken configured.");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(5), ct);
        if (!_o.DryRun && _o.SiteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var ok = await telegram.SetWebhookAsync($"{_o.SiteUrl.TrimEnd('/')}/api/telegram/webhook");
            log.LogInformation("Telegram webhook registration: {Result}", ok ? "ok" : "failed");
            await TelegramProfile.SyncAsync(telegram, log);
            using (var scope = scopes.CreateScope())
                await scope.ServiceProvider.GetRequiredService<TelegramBotService>().SyncAllMenusAsync();
        }

        while (!ct.IsCancellationRequested)
        {
            try { await SendDueDigestsAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Weekly digest run failed"); }
            try { await SendAbsentAlertsOncePerDayAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Absent alert run failed"); }
            try { await SendExamSummariesOncePerDayAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Exam summary run failed"); }
            try { await SendPupilRemindersOncePerDayAsync(ct); }
            catch (Exception ex) { log.LogWarning(ex, "Pupil reminder run failed"); }
            await Task.Delay(Tick, ct);
        }
    }

    // Reminder for teachers about pupils who stopped practising: Monday to Saturday, from 16:00 app time, once a day.
    private DateOnly _alertsDoneFor;

    private async Task SendAbsentAlertsOncePerDayAsync(CancellationToken ct)
    {
        var local = DateTime.UtcNow.AddHours(5);
        var day = DateOnly.FromDateTime(local);
        if (local.DayOfWeek == DayOfWeek.Sunday || local.Hour < 16 || local.Hour >= 21 || _alertsDoneFor == day) return;
        _alertsDoneFor = day;

        using var scope = scopes.CreateScope();
        var notifier = scope.ServiceProvider.GetRequiredService<TelegramNotifier>();
        await notifier.SendAbsentAlertsAsync(ct);
        await notifier.SendInactiveTeamAlertsAsync(ct);
    }

    // Evening reminder for pupils whose goal is not met: every day from 17:00 app time, once a day.
    private DateOnly _remindersDoneFor;

    private async Task SendPupilRemindersOncePerDayAsync(CancellationToken ct)
    {
        var local = DateTime.UtcNow.AddHours(5);
        var day = DateOnly.FromDateTime(local);
        if (local.Hour < 17 || local.Hour >= 21 || _remindersDoneFor == day) return;
        _remindersDoneFor = day;

        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TelegramPupilService>().SendRemindersAsync(ct);
    }

    // Exam results for teachers: Monday to Saturday, from 18:00 app time, once a day.
    private DateOnly _examsDoneFor;

    private async Task SendExamSummariesOncePerDayAsync(CancellationToken ct)
    {
        var local = DateTime.UtcNow.AddHours(5);
        var day = DateOnly.FromDateTime(local);
        if (local.DayOfWeek == DayOfWeek.Sunday || local.Hour < 18 || local.Hour >= 21 || _examsDoneFor == day) return;
        _examsDoneFor = day;

        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<TelegramNotifier>().SendExamSummariesAsync(ct);
    }

    private async Task SendDueDigestsAsync(CancellationToken ct)
    {
        var local = DateTime.UtcNow.AddHours(5);
        if (local.DayOfWeek != DayOfWeek.Monday || local.Hour < 9 || local.Hour >= 21) return;

        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bot = scope.ServiceProvider.GetRequiredService<TelegramBotService>();

        var dueBefore = DateTime.UtcNow.AddDays(-5);
        var due = await db.TelegramConnections
            .Where(c => c.ChatId != null && (c.LastDigestAt == null || c.LastDigestAt < dueBefore))
            .ToListAsync(ct);

        foreach (var c in due)
        {
            if (ct.IsCancellationRequested) break;
            if (c.UserId is null) await bot.SendDigestAsync(c);
            else await bot.SendPersonalDigestAsync(c);
            await Task.Delay(100, ct);      // Telegram allows ~30 messages a second; stay far below
        }
    }
}
