using System.Net;
using System.Text;
using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services.Telegram;

// Messages the bot starts by itself for teachers: a pupil asked to join a team (with Accept / Decline buttons) and
// the reminder about pupils who stopped practising. Never throws: a Telegram problem must not break the site.
public class TelegramNotifier(AppDbContext db, TelegramClient telegram, ClassStatsService stats, ILogger<TelegramNotifier> log)
{
    public const int AbsentDays = 3;
    private const int MaxNamesPerTeam = 8;
    private const int MaxTeamsInAlert = 6;

    // A pupil picked a team: tell the team's teacher (every chat they connected) so they can decide on the spot.
    public async Task NotifyPendingAsync(int classId, int learnerId)
    {
        try
        {
            var cls = await db.SchoolClasses.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == classId);
            var learner = await db.Users.AsNoTracking()
                .Where(u => u.UserId == learnerId && u.ClassId == classId && !u.ClassApproved && u.Role == UserRole.Learner)
                .Select(u => new { u.FirstName, u.LastName }).SingleOrDefaultAsync();
            if (cls is null || learner is null) return;

            var chats = await db.TelegramConnections.AsNoTracking()
                .Where(c => c.UserId == cls.TeacherId && c.ChatId != null).ToListAsync();
            var name = $"{learner.FirstName} {learner.LastName}".Trim();
            foreach (var c in chats)
            {
                var (text, keyboard) = PendingMessage(c.Lang, name, cls.Name, classId, learnerId);
                await telegram.SendAsync(c.ChatId!.Value, text, keyboard);
            }
        }
        catch (Exception ex) { log.LogWarning("Telegram pending notification failed: {Type}", ex.GetType().Name); }
    }

    public static (string Text, object Keyboard) PendingMessage(string lang, string learnerName, string teamName, int classId, int learnerId) =>
    (
        TelegramTexts.Get(lang, "pending_new", WebUtility.HtmlEncode(learnerName), WebUtility.HtmlEncode(teamName)),
        new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = TelegramTexts.Get(lang, "btn_approve"), callback_data = $"ap:{classId}:{learnerId}" },
                    new { text = TelegramTexts.Get(lang, "btn_reject"), callback_data = $"rj:{classId}:{learnerId}" }
                }
            }
        }
    );

    // "Who has not practised for 3+ days", per team, to every connected teacher / director; at most once every 3 days.
    public async Task SendAbsentAlertsAsync(CancellationToken ct)
    {
        var dueBefore = DateTime.UtcNow.AddDays(-AbsentDays).AddHours(1);
        var due = await db.TelegramConnections
            .Where(c => c.ChatId != null && c.UserId != null && (c.LastAlertAt == null || c.LastAlertAt < dueBefore))
            .ToListAsync(ct);

        foreach (var c in due)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == c.UserId, ct);
                // Teachers get the pupil-level reminder; a director gets team-level warnings instead (SendInactiveTeamAlertsAsync).
                if (user is null || user.SchoolId is null || user.Role != UserRole.Teacher) continue;

                var text = await BuildAbsentAsync(c.Lang, user);
                if (text is null) continue;

                var result = await telegram.SendAsync(c.ChatId!.Value, text);
                if (result == SendResult.Sent) { c.LastAlertAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
                else if (result == SendResult.Blocked) { db.TelegramConnections.Remove(c); await db.SaveChangesAsync(ct); }
                await Task.Delay(100, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning("Telegram absent alert failed: {Type}", ex.GetType().Name);
            }
        }
    }

    public async Task<string?> BuildAbsentAsync(string lang, User user)
    {
        var classes = await VisibleClasses(user).OrderBy(c => c.Name).Take(MaxTeamsInAlert).ToListAsync();
        var today = DailyProgressService.Today;
        var sb = new StringBuilder();

        foreach (var cls in classes)
        {
            var students = await stats.GetStudentsAsync(cls.ClassId);
            var absent = students
                .Where(s => s.LastActiveDate is null || today.DayNumber - s.LastActiveDate.Value.DayNumber >= AbsentDays)
                .OrderByDescending(s => s.LastActiveDate is null ? int.MaxValue : today.DayNumber - s.LastActiveDate.Value.DayNumber)
                .ToList();
            if (absent.Count == 0) continue;

            var names = absent.Take(MaxNamesPerTeam).Select(s =>
                $"{WebUtility.HtmlEncode(s.Name)} ({(s.LastActiveDate is { } d ? TelegramTexts.Get(lang, "absent_days", today.DayNumber - d.DayNumber) : TelegramTexts.Get(lang, "absent_never"))})");
            var line = string.Join(", ", names);
            if (absent.Count > MaxNamesPerTeam) line += ", " + TelegramTexts.Get(lang, "absent_more", absent.Count - MaxNamesPerTeam);
            sb.Append(TelegramTexts.Get(lang, "absent_line", WebUtility.HtmlEncode(cls.Name), line)).Append('\n');
        }

        return sb.Length == 0 ? null : TelegramTexts.Get(lang, "absent_title") + "\n\n" + sb.ToString().TrimEnd();
    }

    // "Exam results since the last message", per team, to every connected teacher / director (one message a day at most).
    public async Task SendExamSummariesAsync(CancellationToken ct)
    {
        var chats = await db.TelegramConnections.Where(c => c.ChatId != null && c.UserId != null).ToListAsync(ct);
        foreach (var c in chats)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == c.UserId, ct);
                if (user is null || user.SchoolId is null || user.Role is not (UserRole.Teacher or UserRole.Director)) continue;

                // First time: only what happens from now on (no flood of old exams).
                var built = await BuildExamSummaryAsync(c.Lang, user, c.LastExamAt ?? c.ConnectedAt ?? DateTime.UtcNow, ct);
                if (built is null) continue;

                var result = await telegram.SendAsync(c.ChatId!.Value, built.Value.Text);
                if (result == SendResult.Sent) { c.LastExamAt = built.Value.Newest; await db.SaveChangesAsync(ct); }
                else if (result == SendResult.Blocked) { db.TelegramConnections.Remove(c); await db.SaveChangesAsync(ct); }
                await Task.Delay(100, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning("Telegram exam summary failed: {Type}", ex.GetType().Name);
            }
        }
    }

    public async Task<(string Text, DateTime Newest)?> BuildExamSummaryAsync(string lang, User user, DateTime since, CancellationToken ct)
    {
        const int MaxPupilsPerTeam = 12;
        var classes = await VisibleClasses(user).OrderBy(x => x.Name).Take(MaxTeamsInAlert).ToListAsync(ct);
        if (classes.Count == 0) return null;
        var classIds = classes.Select(x => x.ClassId).ToList();

        var results = await db.ExamResults.AsNoTracking()
            .Where(e => e.ClassId != null && classIds.Contains(e.ClassId.Value) && e.TakenAt > since && e.TotalCount > 0)
            .ToListAsync(ct);
        if (results.Count == 0) return null;

        // Only pupils the teacher has approved, still in that team.
        var userIds = results.Select(r => r.UserId).Distinct().ToList();
        var members = (await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.UserId) && u.ClassApproved && u.Role == UserRole.Learner)
            .Select(u => new { u.UserId, u.ClassId, u.FirstName, u.LastName })
            .ToListAsync(ct)).ToDictionary(u => u.UserId);
        results = results.Where(r => members.TryGetValue(r.UserId, out var m) && m.ClassId == r.ClassId).ToList();
        if (results.Count == 0) return null;

        var sb = new StringBuilder(TelegramTexts.Get(lang, "exam_title")).Append("\n\n");
        foreach (var cls in classes)
        {
            var inClass = results.Where(r => r.ClassId == cls.ClassId).GroupBy(r => r.UserId).ToList();
            if (inClass.Count == 0) continue;

            var entries = inClass
                .Select(g => new { Name = $"{members[g.Key].FirstName} {members[g.Key].LastName}".Trim(), Last = g.OrderBy(r => r.TakenAt).Last(), Tries = g.Count() })
                .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            var line = string.Join(", ", entries.Take(MaxPupilsPerTeam).Select(x =>
                TelegramTexts.Get(lang, "exam_entry", Html(x.Name), x.Last.CorrectCount, x.Last.TotalCount,
                    (int)Math.Round(x.Last.CorrectCount * 100.0 / x.Last.TotalCount))
                + (x.Tries > 1 ? TelegramTexts.Get(lang, "exam_retries", x.Tries) : "")));
            if (entries.Count > MaxPupilsPerTeam) line += ", " + TelegramTexts.Get(lang, "exam_more", entries.Count - MaxPupilsPerTeam);
            sb.Append(TelegramTexts.Get(lang, "absent_line", Html(cls.Name), line)).Append('\n');
        }

        var ids = results.Select(r => r.ExamResultId).ToList();
        var missed = await db.ExamMistakes.AsNoTracking()
            .Where(m => ids.Contains(m.ExamResultId))
            .GroupBy(m => m.Word.ToLower())
            .Select(g => new { Word = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count).ThenBy(g => g.Word)
            .Take(5).ToListAsync(ct);
        if (missed.Count > 0)
            sb.Append('\n').Append(TelegramTexts.Get(lang, "exam_missed",
                string.Join(", ", missed.Select(m => $"{Html(m.Word)} ({m.Count})")))).Append('\n');

        return (sb.ToString().TrimEnd(), results.Max(r => r.TakenAt));
    }

    private static string Html(string s) => WebUtility.HtmlEncode(s);

    // The teacher accepted / declined the pupil's request (or removed them): tell the pupil in their own chat.
    // kind: ok | no | removed
    public async Task NotifyPupilDecisionAsync(int pupilId, string kind, string teamName)
    {
        try
        {
            var chats = await db.TelegramConnections.AsNoTracking().Where(c => c.UserId == pupilId && c.ChatId != null).ToListAsync();
            foreach (var c in chats)
                await telegram.SendAsync(c.ChatId!.Value, TelegramTexts.Get(c.Lang, "decision_" + kind, Html(teamName)));
        }
        catch (Exception ex) { log.LogWarning("Telegram pupil decision notification failed: {Type}", ex.GetType().Name); }
    }

    // A teacher asks to join a school: the school's director (if connected) can accept or decline in the chat.
    public async Task NotifyStaffRequestAsync(int requestId)
    {
        try
        {
            var req = await db.StaffRequests.AsNoTracking().SingleOrDefaultAsync(r => r.StaffRequestId == requestId);
            if (req is null || req.Status != StaffRequestStatus.Pending || req.RequestedRole != UserRole.Teacher) return;
            var school = await SchoolForRequestAsync(req);
            if (school is null) return;
            var applicant = await db.Users.AsNoTracking().Where(u => u.UserId == req.UserId)
                .Select(u => new { u.FirstName, u.LastName }).SingleOrDefaultAsync();
            if (applicant is null) return;

            var directorIds = db.Users.Where(u => u.SchoolId == school.SchoolId && u.Role == UserRole.Director).Select(u => u.UserId);
            var chats = await db.TelegramConnections.AsNoTracking()
                .Where(c => c.ChatId != null && c.UserId != null && directorIds.Contains(c.UserId.Value)).ToListAsync();

            var name = $"{applicant.FirstName} {applicant.LastName}".Trim();
            foreach (var c in chats)
            {
                var details = "";
                if (!string.IsNullOrWhiteSpace(req.Phone)) details += TelegramTexts.Get(c.Lang, "details_phone", Html(req.Phone));
                if (!string.IsNullOrWhiteSpace(req.Comment)) details += TelegramTexts.Get(c.Lang, "details_comment", Html(req.Comment.Length > 200 ? req.Comment[..200] : req.Comment));
                var keyboard = new
                {
                    inline_keyboard = new[]
                    {
                        new[]
                        {
                            new { text = TelegramTexts.Get(c.Lang, "btn_approve"), callback_data = $"sa:{requestId}" },
                            new { text = TelegramTexts.Get(c.Lang, "btn_reject"), callback_data = $"sr:{requestId}" }
                        }
                    }
                };
                await telegram.SendAsync(c.ChatId!.Value, TelegramTexts.Get(c.Lang, "staff_new", Html(name), Html(school.Name), details), keyboard);
            }
        }
        catch (Exception ex) { log.LogWarning("Telegram staff request notification failed: {Type}", ex.GetType().Name); }
    }

    // The verified school a staff request names (same region and name); null when it is a new / unknown school.
    public async Task<School?> SchoolForRequestAsync(StaffRequest req)
    {
        var name = req.SchoolName.Trim().ToLower();
        return await db.Schools.AsNoTracking().FirstOrDefaultAsync(s => s.IsVerified && s.Region == req.Region && s.Name.ToLower() == name);
    }

    // Directors: "these teams did nothing this week", at most once a week.
    public async Task SendInactiveTeamAlertsAsync(CancellationToken ct)
    {
        var dueBefore = DateTime.UtcNow.AddDays(-6);
        var due = await db.TelegramConnections
            .Where(c => c.ChatId != null && c.UserId != null && (c.LastTeamAlertAt == null || c.LastTeamAlertAt < dueBefore))
            .ToListAsync(ct);

        foreach (var c in due)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == c.UserId, ct);
                if (user is null || user.SchoolId is null || user.Role != UserRole.Director) continue;

                var classes = await VisibleClasses(user).OrderBy(x => x.Name).ToListAsync(ct);
                var idle = (await stats.GetClassesAsync(classes)).Where(d => d.StudentCount > 0 && d.ActiveWeek == 0).ToList();
                if (idle.Count == 0) continue;

                var sb = new StringBuilder(TelegramTexts.Get(c.Lang, "inactive_title")).Append("\n\n");
                foreach (var d in idle.Take(15))
                    sb.Append(TelegramTexts.Get(c.Lang, "inactive_line", Html(d.Name), Html(d.TeacherName ?? "—"), d.StudentCount)).Append('\n');
                if (idle.Count > 15) sb.Append(TelegramTexts.Get(c.Lang, "absent_more", idle.Count - 15));

                var result = await telegram.SendAsync(c.ChatId!.Value, sb.ToString().TrimEnd());
                if (result == SendResult.Sent) { c.LastTeamAlertAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
                else if (result == SendResult.Blocked) { db.TelegramConnections.Remove(c); await db.SaveChangesAsync(ct); }
                await Task.Delay(100, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning("Telegram idle team alert failed: {Type}", ex.GetType().Name);
            }
        }
    }

    // Teams a teacher works with (a director sees every team of the school).
    public IQueryable<SchoolClass> VisibleClasses(User user) =>
        user.Role == UserRole.Director
            ? db.SchoolClasses.AsNoTracking().Where(c => c.SchoolId == user.SchoolId && !c.IsArchived)
            : db.SchoolClasses.AsNoTracking().Where(c => c.TeacherId == user.UserId && !c.IsArchived);
}
