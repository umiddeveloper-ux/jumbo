using System.Net;
using System.Text;
using System.Text.Json;
using Jumbo.Api.Controllers;
using Jumbo.Api.Data;
using Jumbo.Api.Services;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Services.Telegram;

// What the bot does: connects a chat to a school (/start <code>), sends the report on request (/report) and every
// Monday (WeeklyDigest), switches the language (/lang) and disconnects (/stop).
public class TelegramBotService(AppDbContext db, TelegramClient telegram, ReportService reports, ClassStatsService stats, TelegramNotifier notifier, TelegramPupilService pupils, IOptions<TelegramOptions> options, ILogger<TelegramBotService> log)
{
    private static readonly TimeSpan CodeLifetime = TimeSpan.FromDays(7);
    private readonly TelegramOptions _o = options.Value;

    // ── incoming updates ────────────────────────────────────────────────────
    public async Task HandleUpdateAsync(JsonElement update)
    {
        if (update.TryGetProperty("callback_query", out var cb))
        {
            await HandleCallbackAsync(cb);
            return;
        }
        if (!update.TryGetProperty("message", out var msg)) return;
        if (!msg.TryGetProperty("chat", out var chat) || Str(chat, "type") != "private") return;   // groups are ignored

        var chatId = chat.GetProperty("id").GetInt64();
        var text = Str(msg, "text")?.Trim();
        if (string.IsNullOrEmpty(text) || text[0] != '/') { await HelpAsync(chatId); return; }

        var firstName = msg.TryGetProperty("from", out var from) ? Str(from, "first_name") : null;
        var parts = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0].ToLowerInvariant();
        var at = command.IndexOf('@');
        if (at > 0) command = command[..at];
        var arg = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "/start": await StartAsync(chatId, arg, firstName); break;
            case "/report": await ReportAsync(chatId); break;
            case "/team": await TeamAsync(chatId); break;
            case "/pending": await PendingAsync(chatId); break;
            case "/exams": await ExamsAsync(chatId); break;
            case "/absent": await AbsentAsync(chatId); break;
            case "/month": await ReportAsync(chatId, "month"); break;
            case "/me": await pupils.MeAsync(chatId); break;
            case "/quiz": await pupils.QuizAsync(chatId); break;
            case "/goal": await pupils.GoalAsync(chatId); break;
            case "/top": await pupils.TopAsync(chatId); break;
            case "/play": await pupils.PlayAsync(chatId); break;
            case "/remind": await pupils.RemindAsync(chatId); break;
            case "/teachers": await TeachersAsync(chatId); break;
            case "/lang": await AskLanguageAsync(chatId); break;
            case "/stop": await StopAsync(chatId); break;
            default: await HelpAsync(chatId); break;
        }
    }

    private async Task StartAsync(long chatId, string? code, string? firstName)
    {
        var lang = await LangOfAsync(chatId);
        if (string.IsNullOrWhiteSpace(code))
        {
            await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "welcome"));
            return;
        }

        code = code.Trim();
        var since = DateTime.UtcNow - CodeLifetime;
        var link = code.Length is >= 8 and <= 32
            ? await db.TelegramConnections.SingleOrDefaultAsync(c => c.Code == code && c.ChatId == null && c.CreatedAt > since)
            : null;
        if (link is null)
        {
            await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "badcode"));
            return;
        }

        // The same person connecting again for the same school keeps one row (per kind: personal / recipient).
        var personal = link.UserId is not null;
        var existing = await db.TelegramConnections
            .Where(c => c.ChatId == chatId && (personal ? c.UserId == link.UserId : (c.UserId == null && c.SchoolId == link.SchoolId)))
            .ToListAsync();
        db.TelegramConnections.RemoveRange(existing);

        link.ChatId = chatId;
        link.TelegramName = firstName is { Length: > 100 } ? firstName[..100] : firstName;
        link.ConnectedAt = DateTime.UtcNow;
        link.LastExamAt = DateTime.UtcNow;        // exams are reported from now on
        if (personal)
        {
            // A pupil gets the bot in the language of their app.
            var appLang = await db.Users.AsNoTracking().Where(u => u.UserId == link.UserId && u.Role == UserRole.Learner)
                .Select(u => (int?)u.AppLanguageId).SingleOrDefaultAsync();
            if (appLang is { } a) lang = a switch { 2 => "ru", 3 => "uz", _ => "en" };
        }
        link.Lang = lang;
        await db.SaveChangesAsync();
        await ApplyMenuAsync(chatId);

        if (personal)
        {
            var me = await db.Users.AsNoTracking().Where(u => u.UserId == link.UserId).Select(u => new { u.FirstName, u.LastName, u.Role }).SingleAsync();
            if (me.Role == UserRole.Learner)
            {
                await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "pupil_connected", Esc(me.FirstName)));
                return;
            }
            var role = TelegramTexts.Get(lang, me.Role == UserRole.Director ? "role_director" : "role_teacher");
            await telegram.SendAsync(chatId, TelegramTexts.Get(lang, me.Role == UserRole.Director ? "connected_dir" : "connected_me", Esc($"{me.FirstName} {me.LastName}".Trim()), role));
            return;
        }

        var school = await db.Schools.AsNoTracking().SingleAsync(s => s.SchoolId == link.SchoolId);
        await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "connected", Esc(school.Name)));
        await SendDigestAsync(link);
    }

    private async Task ReportAsync(long chatId, string period = "week")
    {
        var connections = await db.TelegramConnections.Where(c => c.ChatId == chatId && c.SchoolId != null).ToListAsync();
        if (connections.Count == 0)
        {
            // A pupil's chat has a link too, but no school report: say so instead of "not connected".
            var hasLink = await db.TelegramConnections.AsNoTracking().AnyAsync(c => c.ChatId == chatId);
            var lang = await LangOfAsync(chatId);
            await telegram.SendAsync(chatId, TelegramTexts.Get(lang, hasLink ? "staff_only" : "notconnected"));
            return;
        }
        foreach (var c in connections) await SendDigestAsync(c, period);
    }

    private async Task StopAsync(long chatId)
    {
        var lang = await LangOfAsync(chatId);
        var rows = await db.TelegramConnections.Where(c => c.ChatId == chatId).ToListAsync();
        if (rows.Count == 0)
        {
            await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "notconnected"));
            return;
        }
        db.TelegramConnections.RemoveRange(rows);
        await db.SaveChangesAsync();
        await ApplyMenuAsync(chatId);
        await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "stopped"));
    }

    private Task HelpAsync(long chatId) => LangOfAsync(chatId).ContinueWith(t => telegram.SendAsync(chatId, TelegramTexts.Get(t.Result, "help"))).Unwrap();

    private async Task AskLanguageAsync(long chatId)
    {
        var lang = await LangOfAsync(chatId);
        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = "Русский", callback_data = "lang:ru" },
                    new { text = "O'zbekcha", callback_data = "lang:uz" },
                    new { text = "English", callback_data = "lang:en" }
                }
            }
        };
        await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "lang_ask"), keyboard);
    }

    private async Task HandleCallbackAsync(JsonElement cb)
    {
        var id = Str(cb, "id");
        var data = Str(cb, "data");
        if (data is not null && cb.TryGetProperty("message", out var cbMsg) && cbMsg.TryGetProperty("message_id", out var cbMsgId))
        {
            var info = new CallbackInfo(id ?? "", cbMsg.GetProperty("chat").GetProperty("id").GetInt64(), cbMsgId.GetInt64());
            if (data.StartsWith("g:")) { await pupils.SetGoalAsync(info, data[2..]); return; }
            if (data.StartsWith("qz:")) { await pupils.QuizAnswerAsync(info, data[3..]); return; }
            if (data == "play") { await telegram.AnswerCallbackAsync(info.CallbackId); await pupils.PlayAsync(info.ChatId); return; }
            if (data == "quiz") { await telegram.AnswerCallbackAsync(info.CallbackId); await pupils.QuizAsync(info.ChatId); return; }
        }
        if (data is not null && (data.StartsWith("sa:") || data.StartsWith("sr:")))
        {
            await DecideStaffAsync(cb, id, approve: data.StartsWith("sa:"), data[3..]);
            return;
        }
        if (data is not null && (data.StartsWith("ap:") || data.StartsWith("rj:")))
        {
            await DecideAsync(cb, id, approve: data.StartsWith("ap:"), data[3..]);
            return;
        }

        if (id is not null) await telegram.AnswerCallbackAsync(id);
        if (data is null || !data.StartsWith("lang:") || !cb.TryGetProperty("message", out var msg)) return;

        var lang = data[5..];
        if (Array.IndexOf(TelegramTexts.Languages, lang) < 0) return;
        var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();

        await db.TelegramConnections.Where(c => c.ChatId == chatId).ExecuteUpdateAsync(s => s.SetProperty(c => c.Lang, lang));
        await ApplyMenuAsync(chatId);
        await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "lang_set"));
    }

    // ── teachers: the staff account behind a chat ───────────────────────────
    private async Task<User?> StaffOfChatAsync(long chatId)
    {
        var userId = await db.TelegramConnections.AsNoTracking()
            .Where(c => c.ChatId == chatId && c.UserId != null).Select(c => c.UserId).FirstOrDefaultAsync();
        if (userId is null) return null;
        return await db.Users.AsNoTracking().SingleOrDefaultAsync(u =>
            u.UserId == userId && u.SchoolId != null && (u.Role == UserRole.Teacher || u.Role == UserRole.Director));
    }

    // /team: every team of the teacher (of the school, for a director) in a few lines.
    private async Task TeamAsync(long chatId)
    {
        var lang = await LangOfAsync(chatId);
        var me = await StaffOfChatAsync(chatId);
        if (me is null) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "staff_only")); return; }

        var classes = await notifier.VisibleClasses(me).OrderBy(c => c.Name).ToListAsync();
        if (classes.Count == 0) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "team_none")); return; }

        var sb = new StringBuilder(TelegramTexts.Get(lang, "team_title")).Append("\n\n");
        foreach (var c in await stats.GetClassesAsync(classes))
        {
            sb.Append(TelegramTexts.Get(lang, "team_line", Esc(c.Name), c.StudentCount, c.ActiveWeek));
            if (c.PendingCount > 0) sb.Append(TelegramTexts.Get(lang, "team_pending", c.PendingCount));
            if (c.AvgExamPercent is { } p) sb.Append(TelegramTexts.Get(lang, "team_exam", p));
            sb.Append('\n');
        }
        sb.Append('\n').Append(TelegramTexts.Get(lang, "team_cabinet")).Append(' ').Append(_o.SiteUrl.TrimEnd('/')).Append("/teacher");
        await telegram.SendAsync(chatId, sb.ToString());
    }

    // /exams: exam results of the last 7 days on demand (does not touch the "already reported" mark of the daily message).
    private async Task ExamsAsync(long chatId)
    {
        var lang = await LangOfAsync(chatId);
        var me = await StaffOfChatAsync(chatId);
        if (me is null) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "staff_only")); return; }

        var built = await notifier.BuildExamSummaryAsync(lang, me, DateTime.UtcNow.AddDays(-7), CancellationToken.None);
        await telegram.SendAsync(chatId, built is null
            ? TelegramTexts.Get(lang, "exam_none_week")
            : built.Value.Text.Replace("</b>\n\n", "</b> " + TelegramTexts.Get(lang, "exam_period_note") + "\n\n"));
    }

    // /absent: who has not practised for 3+ days, on demand.
    private async Task AbsentAsync(long chatId)
    {
        var lang = await LangOfAsync(chatId);
        var me = await StaffOfChatAsync(chatId);
        if (me is null) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "staff_only")); return; }

        await telegram.SendAsync(chatId, await notifier.BuildAbsentAsync(lang, me) ?? TelegramTexts.Get(lang, "absent_none"));
    }

    // /pending: each waiting pupil as its own message with Accept / Decline buttons (the first few).
    private async Task PendingAsync(long chatId)
    {
        const int Max = 10;
        var lang = await LangOfAsync(chatId);
        var me = await StaffOfChatAsync(chatId);
        if (me is null) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "staff_only")); return; }

        var classes = await notifier.VisibleClasses(me).OrderBy(c => c.Name).ToListAsync();
        var shown = 0;
        var total = 0;
        foreach (var cls in classes)
        {
            foreach (var p in await stats.GetPendingAsync(cls.ClassId))
            {
                total++;
                if (shown >= Max) continue;
                var (text, keyboard) = TelegramNotifier.PendingMessage(lang, p.Name, cls.Name, cls.ClassId, p.UserId);
                await telegram.SendAsync(chatId, text, keyboard);
                shown++;
            }
        }
        if (total == 0) await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "pending_none"));
        else if (total > shown) await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "pending_more", total - shown));
    }

    // The Accept / Decline buttons: the same effect as the cabinet (approve keeps the team, decline clears it).
    private async Task DecideAsync(JsonElement cb, string? callbackId, bool approve, string args)
    {
        async Task Answer(string? text, bool alert = false) { if (callbackId is not null) await telegram.AnswerCallbackAsync(callbackId, text, alert); }

        if (!cb.TryGetProperty("message", out var msg)) { await Answer(null); return; }
        var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
        var messageId = msg.GetProperty("message_id").GetInt64();
        var lang = await LangOfAsync(chatId);

        var parts = args.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var classId) || !int.TryParse(parts[1], out var learnerId))
        {
            await Answer(null);
            return;
        }

        // Only the connected teacher of this team (or a director of its school) may decide.
        var me = await StaffOfChatAsync(chatId);
        var cls = me is null ? null : await notifier.VisibleClasses(me).SingleOrDefaultAsync(c => c.ClassId == classId);
        if (cls is null) { await Answer(TelegramTexts.Get(lang, "toast_denied"), alert: true); return; }

        var learner = await db.Users.SingleOrDefaultAsync(u => u.UserId == learnerId && u.ClassId == classId && u.Role == UserRole.Learner);
        if (learner is null || learner.ClassApproved)
        {
            await telegram.EditAsync(chatId, messageId, TelegramTexts.Get(lang, "decided_already"));
            await Answer(null);
            return;
        }

        var name = $"{learner.FirstName} {learner.LastName}".Trim();
        if (approve) learner.ClassApproved = true;
        else ClassesController.ClearTeam(learner);
        await db.SaveChangesAsync();

        await notifier.NotifyPupilDecisionAsync(learnerId, approve ? "ok" : "no", cls.Name);
        await telegram.EditAsync(chatId, messageId,
            TelegramTexts.Get(lang, approve ? "decided_ok" : "decided_no", Esc(name), Esc(cls.Name)));
        await Answer(TelegramTexts.Get(lang, approve ? "toast_ok" : "toast_no"));
    }

    // The Monday message for a personally connected account: a teacher gets figures for their own teams,
    // a director the school report (the same one a report recipient gets).
    public async Task<SendResult> SendPersonalDigestAsync(TelegramConnection c)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == c.UserId);
        if (user is null || user.SchoolId is null) return SendResult.Failed;
        if (user.Role == UserRole.Director) return await SendDigestAsync(c);
        if (user.Role != UserRole.Teacher || c.ChatId is not { } chatId) return SendResult.Failed;

        var classes = await notifier.VisibleClasses(user).OrderBy(x => x.Name).Take(6).ToListAsync();
        if (classes.Count == 0) return SendResult.Failed;

        var sb = new StringBuilder(TelegramTexts.Get(c.Lang, "tdigest_title")).Append("\n\n");
        foreach (var cls in classes)
        {
            var report = await reports.BuildTeamAsync(cls, "week");
            var t = report.Totals;
            if (t.Students == 0) continue;
            sb.Append(TelegramTexts.Get(c.Lang, "tdigest_team", Esc(cls.Name), t.Active, t.Students, t.ActivePrev, t.Words, t.WordsPrev)).Append('\n');
            if (t.Students - t.Active > 0) sb.Append(TelegramTexts.Get(c.Lang, "tdigest_absent", t.Students - t.Active)).Append('\n');
            sb.Append('\n');
        }
        if (sb.Length == TelegramTexts.Get(c.Lang, "tdigest_title").Length + 2) return SendResult.Failed;   // no pupils anywhere: nothing to say
        sb.Append(TelegramTexts.Get(c.Lang, "tdigest_link")).Append('\n').Append(_o.SiteUrl.TrimEnd('/')).Append("/teacher");

        var result = await telegram.SendAsync(chatId, sb.ToString());
        if (result == SendResult.Sent) { c.LastDigestAt = DateTime.UtcNow; await db.SaveChangesAsync(); }
        else if (result == SendResult.Blocked) { db.TelegramConnections.Remove(c); await db.SaveChangesAsync(); }
        return result;
    }

    // ── director: teachers, staff requests, per-chat menu ───────────────────
    private async Task TeachersAsync(long chatId)
    {
        var lang = await LangOfAsync(chatId);
        var me = await StaffOfChatAsync(chatId);
        if (me is null) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "staff_only")); return; }
        if (me.Role != UserRole.Director) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "director_only")); return; }

        var classes = await notifier.VisibleClasses(me).ToListAsync();
        if (classes.Count == 0) { await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "teachers_none")); return; }

        var sb = new StringBuilder(TelegramTexts.Get(lang, "teachers_title")).Append("\n\n");
        var groups = (await stats.GetClassesAsync(classes)).GroupBy(c => c.TeacherName ?? "—").OrderBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase);
        foreach (var g in groups)
        {
            sb.Append(TelegramTexts.Get(lang, "teachers_line", Esc(g.Key), g.Count(), g.Sum(c => c.StudentCount), g.Sum(c => c.ActiveWeek)));
            var pending = g.Sum(c => c.PendingCount);
            if (pending > 0) sb.Append(TelegramTexts.Get(lang, "team_pending", pending));
            sb.Append('\n');
        }
        await telegram.SendAsync(chatId, sb.ToString().TrimEnd());
    }

    // Accept / Decline of a teacher's request to join the school (only that school's director, only teacher requests).
    private async Task DecideStaffAsync(JsonElement cb, string? callbackId, bool approve, string arg)
    {
        async Task Answer(string? text, bool alert = false) { if (callbackId is not null) await telegram.AnswerCallbackAsync(callbackId, text, alert); }

        if (!cb.TryGetProperty("message", out var msg)) { await Answer(null); return; }
        var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
        var messageId = msg.GetProperty("message_id").GetInt64();
        var lang = await LangOfAsync(chatId);

        if (!int.TryParse(arg, out var requestId)) { await Answer(null); return; }
        var me = await StaffOfChatAsync(chatId);
        if (me is null || me.Role != UserRole.Director) { await Answer(TelegramTexts.Get(lang, "toast_denied"), alert: true); return; }

        var req = await db.StaffRequests.SingleOrDefaultAsync(r => r.StaffRequestId == requestId);
        var school = req is null ? null : await notifier.SchoolForRequestAsync(req);
        if (req is null || school is null || school.SchoolId != me.SchoolId || req.RequestedRole != UserRole.Teacher)
        {
            await Answer(TelegramTexts.Get(lang, "toast_denied"), alert: true);
            return;
        }

        var applicant = await db.Users.SingleAsync(u => u.UserId == req.UserId);
        if (req.Status != StaffRequestStatus.Pending || applicant.Role is UserRole.Teacher or UserRole.Director or UserRole.Admin)
        {
            await telegram.EditAsync(chatId, messageId, TelegramTexts.Get(lang, "decided_already"));
            await Answer(null);
            return;
        }

        var name = $"{applicant.FirstName} {applicant.LastName}".Trim();
        if (approve) StaffApproval.Approve(req, applicant, school, me.UserId, null);
        else StaffApproval.Reject(req, me.UserId, null);
        await db.SaveChangesAsync();

        await telegram.EditAsync(chatId, messageId, approve
            ? TelegramTexts.Get(lang, "staff_ok", Esc(name), Esc(school.Name))
            : TelegramTexts.Get(lang, "staff_no", Esc(name)));
        await Answer(TelegramTexts.Get(lang, approve ? "toast_ok" : "toast_no"));
    }

    // Menu of one chat: nothing special for a stranger (the default: /start, /lang), the role's commands once connected.
    public async Task ApplyMenuAsync(long chatId)
    {
        var rows = await db.TelegramConnections.AsNoTracking().Where(c => c.ChatId == chatId).ToListAsync();
        if (rows.Count == 0) { await telegram.DeleteChatCommandsAsync(chatId); return; }

        var kind = "recipient";
        var personal = rows.FirstOrDefault(c => c.UserId != null);
        if (personal is not null)
        {
            var role = await db.Users.AsNoTracking().Where(u => u.UserId == personal.UserId).Select(u => u.Role).SingleOrDefaultAsync();
            kind = role == UserRole.Director ? "director" : role == UserRole.Teacher ? "teacher" : role == UserRole.Learner ? "pupil" : "recipient";
        }
        await telegram.SetChatCommandsAsync(chatId, TelegramProfile.MenuFor(kind, rows[0].Lang));
    }

    // At start: chats connected before this feature get their menu too.
    public async Task SyncAllMenusAsync()
    {
        var chats = await db.TelegramConnections.AsNoTracking().Where(c => c.ChatId != null).Select(c => c.ChatId!.Value).Distinct().ToListAsync();
        foreach (var chat in chats)
        {
            await ApplyMenuAsync(chat);
            await Task.Delay(50);
        }
    }

    // ── report messages (also used by the weekly job) ───────────────────────
    public async Task<SendResult> SendDigestAsync(TelegramConnection c, string period = "week")
    {
        if (c.ChatId is not { } chatId) return SendResult.Failed;

        var school = await db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.SchoolId == c.SchoolId);
        if (school is null) return SendResult.Failed;

        var report = await reports.BuildSchoolAsync(school, period);
        var m = period == "month" ? "_m" : "";
        var link = await ActiveReportLinkAsync(c);
        var t = report.Totals;

        var sb = new StringBuilder();
        sb.Append("<b>").Append(TelegramTexts.Get(c.Lang, period == "month" ? "digest_title_month" : "digest_title", Esc(school.Name))).Append("</b>\n\n");
        if (t.Students == 0)
        {
            sb.Append(TelegramTexts.Get(c.Lang, "digest_empty")).Append('\n');
        }
        else
        {
            sb.Append(TelegramTexts.Get(c.Lang, "digest_students", t.Students)).Append('\n');
            sb.Append(TelegramTexts.Get(c.Lang, "digest_active" + m, t.Active, t.ActivePrev)).Append('\n');
            sb.Append(TelegramTexts.Get(c.Lang, "digest_words" + m, t.Words, t.WordsPrev)).Append('\n');
            if (t.ExamCount > 0 && t.AvgExamPercent is { } avg)
                sb.Append(TelegramTexts.Get(c.Lang, "digest_exams", t.ExamCount, avg)).Append('\n');
        }
        sb.Append('\n').Append(TelegramTexts.Get(c.Lang, "digest_link")).Append('\n')
          .Append($"{_o.SiteUrl.TrimEnd('/')}/r/{link.Token}");

        var result = await telegram.SendAsync(chatId, sb.ToString());
        if (result == SendResult.Sent && period == "week")
        {
            c.LastDigestAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        else if (result == SendResult.Blocked)
        {
            log.LogInformation("Telegram chat blocked the bot: connection {Id} removed", c.TelegramConnectionId);
            db.TelegramConnections.Remove(c);
            await db.SaveChangesAsync();
        }
        return result;
    }

    // The school's secret report link; created on demand (the same link the cabinet shows).
    private async Task<SchoolReportLink> ActiveReportLinkAsync(TelegramConnection c)
    {
        var link = await db.SchoolReportLinks.Where(l => l.SchoolId == c.SchoolId && l.RevokedAt == null)
            .OrderByDescending(l => l.CreatedAt).FirstOrDefaultAsync();
        if (link is not null) return link;

        link = new SchoolReportLink
        {
            SchoolId = c.SchoolId!.Value,
            CreatedById = c.CreatedById,
            Token = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=')
        };
        db.SchoolReportLinks.Add(link);
        await db.SaveChangesAsync();
        return link;
    }

    // ── helpers ─────────────────────────────────────────────────────────────
    private async Task<string> LangOfAsync(long chatId) =>
        await db.TelegramConnections.AsNoTracking().Where(c => c.ChatId == chatId).Select(c => c.Lang).FirstOrDefaultAsync() ?? "ru";

    private static string Esc(string s) => WebUtility.HtmlEncode(s);

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
