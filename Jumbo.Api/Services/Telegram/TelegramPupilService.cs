using System.Net;
using System.Security.Cryptography;
using System.Text;
using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Jumbo.Api.Services.Telegram;

// What a connected pupil can do in the bot: /me (progress), /quiz (5 questions, correct words count towards today's goal),
// /goal, /top, /play (opens the site signed in), /remind, and the evening reminder. The pupil's chat is linked to their
// own account from the app / site settings, so everything shown here is about that one person.
public class TelegramPupilService(
    AppDbContext db, TelegramClient telegram, DailyProgressService daily, LearnerStatsService learner,
    WebLinkService webLinks, IMemoryCache cache, IOptions<TelegramOptions> options, ILogger<TelegramPupilService> log)
{
    private const int QuizLength = 5;
    private static readonly TimeSpan QuizLifetime = TimeSpan.FromMinutes(20);
    private readonly TelegramOptions _o = options.Value;

    public sealed record Pupil(User User, string Lang);

    // The learner behind a chat (a chat linked from a pupil's settings); null for staff and strangers.
    public async Task<Pupil?> PupilOfChatAsync(long chatId)
    {
        var conn = await db.TelegramConnections.AsNoTracking().Where(c => c.ChatId == chatId && c.UserId != null).FirstOrDefaultAsync();
        if (conn is null) return null;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == conn.UserId && u.Role == UserRole.Learner);
        return user is null ? null : new Pupil(user, conn.Lang);
    }

    private async Task<Pupil?> RequirePupilAsync(long chatId)
    {
        var pupil = await PupilOfChatAsync(chatId);
        if (pupil is null)
        {
            var lang = await db.TelegramConnections.AsNoTracking().Where(c => c.ChatId == chatId).Select(c => c.Lang).FirstOrDefaultAsync() ?? "ru";
            await telegram.SendAsync(chatId, TelegramTexts.Get(lang, "pupil_only"));
        }
        return pupil;
    }

    // ── /me ─────────────────────────────────────────────────────────────────
    public async Task MeAsync(long chatId)
    {
        if (await RequirePupilAsync(chatId) is not { } p) return;
        var l = p.Lang;
        var day = await daily.GetAsync(p.User.UserId);
        var level = await learner.GetLevelAsync(p.User.UserId);
        var (scope, place) = await learner.GetPlaceAsync(p.User, 1);

        var sb = new StringBuilder();
        sb.Append(TelegramTexts.Get(l, "me_title", Esc($"{p.User.FirstName} {p.User.LastName}".Trim()))).Append("\n\n");
        sb.Append(TelegramTexts.Get(l, "me_level", level)).Append('\n');
        sb.Append(TelegramTexts.Get(l, "me_streak", day.Streak)).Append('\n');
        sb.Append(TelegramTexts.Get(l, "me_today", day.TodayWords, day.GoalWords, day.GoalMetToday ? " ✅" : "")).Append('\n');
        sb.Append(TelegramTexts.Get(l, "me_lp", p.User.LpPoints)).Append('\n');
        sb.Append(place.Me is { } me
            ? TelegramTexts.Get(l, "me_rank", TelegramTexts.Get(l, "scope_" + scope), me.Rank, place.Total)
            : TelegramTexts.Get(l, "me_norank"));

        await telegram.SendAsync(chatId, sb.ToString(), PlayKeyboard(l, quiz: true));
    }

    // ── /top ────────────────────────────────────────────────────────────────
    public async Task TopAsync(long chatId)
    {
        if (await RequirePupilAsync(chatId) is not { } p) return;
        var l = p.Lang;
        var (scope, data) = await learner.GetPlaceAsync(p.User, 5);
        if (data.Entries.Count == 0) { await telegram.SendAsync(chatId, TelegramTexts.Get(l, "top_empty")); return; }

        var sb = new StringBuilder(TelegramTexts.Get(l, "top_title", TelegramTexts.Get(l, "scope_" + scope))).Append("\n\n");
        foreach (var e in data.Entries)
        {
            var name = e.Name is null ? TelegramTexts.Get(l, "top_hidden") : Esc(e.Name);
            sb.Append(TelegramTexts.Get(l, "top_line", e.Rank, name, e.Words));
            if (e.IsMe) sb.Append(' ').Append(TelegramTexts.Get(l, "top_me_tag"));
            sb.Append('\n');
        }
        if (data.Me is { } me && !data.Entries.Any(e => e.IsMe))
            sb.Append("…\n").Append(TelegramTexts.Get(l, "top_me_row", me.Rank, me.Words)).Append('\n');
        await telegram.SendAsync(chatId, sb.ToString().TrimEnd());
    }

    // ── /goal ───────────────────────────────────────────────────────────────
    public async Task GoalAsync(long chatId)
    {
        if (await RequirePupilAsync(chatId) is not { } p) return;
        var buttons = DailyProgressService.AllowedGoals
            .Select(g => new { text = (g == p.User.DailyGoalWords ? "✓ " : "") + TelegramTexts.Get(p.Lang, "goal_btn", g), callback_data = $"g:{g}" })
            .ToArray();
        await telegram.SendAsync(chatId, TelegramTexts.Get(p.Lang, "goal_ask", p.User.DailyGoalWords), new { inline_keyboard = new[] { buttons } });
    }

    public async Task SetGoalAsync(CallbackInfo cb, string arg)
    {
        var p = await PupilOfChatAsync(cb.ChatId);
        if (p is null || !int.TryParse(arg, out var goal) || !DailyProgressService.AllowedGoals.Contains(goal))
        {
            await telegram.AnswerCallbackAsync(cb.CallbackId);
            return;
        }
        await daily.SetGoalAsync(p.User.UserId, goal);
        await telegram.EditAsync(cb.ChatId, cb.MessageId, TelegramTexts.Get(p.Lang, "goal_set", goal));
        await telegram.AnswerCallbackAsync(cb.CallbackId, TelegramTexts.Get(p.Lang, "goal_set_toast"));
    }

    // ── /play: a button that opens the site, signed in (the code lives for a minute, so it is made on request) ──
    public async Task PlayAsync(long chatId)
    {
        if (await RequirePupilAsync(chatId) is not { } p) return;
        var session = await webLinks.CreateAsync(p.User);
        var url = $"{_o.SiteUrl.TrimEnd('/')}/go/{session.SessionId}?next=/little-polyglot";
        var keyboard = new { inline_keyboard = new[] { new[] { new { text = TelegramTexts.Get(p.Lang, "play_btn"), url } } } };
        await telegram.SendAsync(chatId, TelegramTexts.Get(p.Lang, "play_text"), keyboard);
    }

    // ── /remind ─────────────────────────────────────────────────────────────
    public async Task RemindAsync(long chatId)
    {
        if (await RequirePupilAsync(chatId) is not { } p) return;
        var rows = await db.TelegramConnections.Where(c => c.ChatId == chatId && c.UserId == p.User.UserId).ToListAsync();
        var off = !rows.All(r => r.NotifyOff);          // everything on -> switch off; otherwise switch on
        foreach (var r in rows) r.NotifyOff = off;
        await db.SaveChangesAsync();
        await telegram.SendAsync(chatId, TelegramTexts.Get(p.Lang, off ? "remind_off" : "remind_on"));
    }

    // ── /quiz ───────────────────────────────────────────────────────────────
    private sealed record Question(int WordId, string Word, string[] Options, int Correct);

    private sealed class Quiz
    {
        public int UserId;
        public long ChatId;
        public List<Question> Questions = new();
        public int Step;
        public int Score;
        public List<int> RightWordIds = new();
    }

    public async Task QuizAsync(long chatId)
    {
        if (await RequirePupilAsync(chatId) is not { } p) return;
        var pool = await PoolAsync(p.User.StudyLanguageId, p.User.TranslationLanguageId);
        if (pool.Count < 8) { await telegram.SendAsync(chatId, TelegramTexts.Get(p.Lang, "quiz_nowords")); return; }

        var quiz = new Quiz { UserId = p.User.UserId, ChatId = chatId };
        foreach (var w in pool.OrderBy(_ => Random.Shared.Next()).Take(QuizLength))
        {
            var options = new List<string> { w.Translation };
            for (var guard = 0; options.Count < 4 && guard < 60; guard++)
            {
                var other = pool[Random.Shared.Next(pool.Count)].Translation;
                if (!options.Contains(other, StringComparer.OrdinalIgnoreCase)) options.Add(other);
            }
            var shuffled = options.OrderBy(_ => Random.Shared.Next()).ToArray();
            quiz.Questions.Add(new Question(w.WordId, w.Study, shuffled, Array.IndexOf(shuffled, w.Translation)));
        }

        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        cache.Set($"quiz:{id}", quiz, QuizLifetime);
        await SendQuestionAsync(id, quiz, p.Lang);
    }

    private async Task SendQuestionAsync(string id, Quiz quiz, string lang)
    {
        var q = quiz.Questions[quiz.Step];
        var keyboard = new
        {
            inline_keyboard = q.Options.Select((o, i) => new[] { new { text = o, callback_data = $"qz:{id}:{quiz.Step}:{i}" } }).ToArray()
        };
        await telegram.SendAsync(quiz.ChatId, TelegramTexts.Get(lang, "quiz_q", quiz.Step + 1, QuizLength, Esc(q.Word)), keyboard);
    }

    public async Task QuizAnswerAsync(CallbackInfo cb, string arg)
    {
        var parts = arg.Split(':');
        var p = await PupilOfChatAsync(cb.ChatId);
        if (p is null || parts.Length != 3 || !int.TryParse(parts[1], out var step) || !int.TryParse(parts[2], out var choice))
        {
            await telegram.AnswerCallbackAsync(cb.CallbackId);
            return;
        }
        var l = p.Lang;

        if (!cache.TryGetValue($"quiz:{parts[0]}", out Quiz? quiz) || quiz is null || quiz.UserId != p.User.UserId || quiz.ChatId != cb.ChatId)
        {
            await telegram.EditAsync(cb.ChatId, cb.MessageId, TelegramTexts.Get(l, "quiz_expired"));
            await telegram.AnswerCallbackAsync(cb.CallbackId);
            return;
        }
        if (step != quiz.Step)
        {
            await telegram.AnswerCallbackAsync(cb.CallbackId, TelegramTexts.Get(l, "quiz_already"));
            return;
        }

        var q = quiz.Questions[quiz.Step];
        var right = choice == q.Correct;
        if (right) { quiz.Score++; quiz.RightWordIds.Add(q.WordId); }
        quiz.Step++;

        await telegram.EditAsync(cb.ChatId, cb.MessageId, right
            ? TelegramTexts.Get(l, "quiz_ok", Esc(q.Word), Esc(q.Options[q.Correct]))
            : TelegramTexts.Get(l, "quiz_bad", Esc(q.Word), Esc(q.Options[q.Correct])));
        await telegram.AnswerCallbackAsync(cb.CallbackId);

        if (quiz.Step < QuizLength) { await SendQuestionAsync(parts[0], quiz, l); return; }

        cache.Remove($"quiz:{parts[0]}");
        await FinishQuizAsync(quiz, l);
    }

    // Correct answers are practised words: they count towards today's goal and the streak, like a won game.
    private async Task FinishQuizAsync(Quiz quiz, string lang)
    {
        var before = (await daily.GetAsync(quiz.UserId)).TodayWords;
        var after = await daily.GetAsync(quiz.UserId);
        if (quiz.RightWordIds.Count > 0)
        {
            var known = (await db.UsersWords.AsNoTracking()
                .Where(uw => uw.UserId == quiz.UserId && quiz.RightWordIds.Contains(uw.WordId))
                .Select(uw => uw.WordId).ToListAsync()).ToHashSet();
            foreach (var wordId in quiz.RightWordIds.Where(w => !known.Contains(w)))
                db.UsersWords.Add(new UsersWord { UserId = quiz.UserId, WordId = wordId, RepeatingCount = 0 });
            await db.SaveChangesAsync();
            after = await daily.RecordWordsAsync(quiz.UserId, quiz.RightWordIds);
        }

        var emoji = quiz.Score == QuizLength ? "🎉" : quiz.Score >= 3 ? "👍" : "💪";
        var keyboard = new
        {
            inline_keyboard = new[]
            {
                new[]
                {
                    new { text = TelegramTexts.Get(lang, "quiz_again"), callback_data = "quiz" },
                    new { text = TelegramTexts.Get(lang, "play_btn"), callback_data = "play" }
                }
            }
        };
        await telegram.SendAsync(quiz.ChatId,
            TelegramTexts.Get(lang, "quiz_done", quiz.Score, QuizLength, emoji, after.TodayWords - before, after.TodayWords, after.GoalWords), keyboard);
    }

    // Study words with their translation, cached (about 1.5k rows per language pair).
    private async Task<List<(int WordId, string Study, string Translation)>> PoolAsync(int studyLang, int transLang)
    {
        var key = $"quizpool:{studyLang}:{transLang}";
        if (cache.TryGetValue(key, out List<(int, string, string)>? cached) && cached is not null) return cached;

        var study = await db.Words.AsNoTracking().Where(w => w.LanguageId == studyLang).Select(w => new { w.WordId, w.SvgImageId, w.WordName }).ToListAsync();
        var trans = (await db.Words.AsNoTracking().Where(w => w.LanguageId == transLang).Select(w => new { w.SvgImageId, w.WordName }).ToListAsync())
            .GroupBy(w => w.SvgImageId).ToDictionary(g => g.Key, g => g.First().WordName);
        var pool = study.Where(s => trans.ContainsKey(s.SvgImageId)).Select(s => (s.WordId, s.WordName, trans[s.SvgImageId])).ToList();
        cache.Set(key, pool, TimeSpan.FromMinutes(30));
        return pool;
    }

    // ── evening reminder ────────────────────────────────────────────────────
    public async Task SendRemindersAsync(CancellationToken ct)
    {
        var todayLocal = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5));
        var rows = await db.TelegramConnections.Where(c => c.ChatId != null && c.UserId != null && !c.NotifyOff).ToListAsync(ct);

        foreach (var c in rows)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                if (c.LastReminderAt is { } last && DateOnly.FromDateTime(last.AddHours(5)) == todayLocal) continue;
                var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.UserId == c.UserId && u.Role == UserRole.Learner, ct);
                if (user is null) continue;

                var day = await daily.GetAsync(user.UserId);
                if (day.GoalMetToday) continue;

                var left = Math.Max(0, day.GoalWords - day.TodayWords);
                var text = day.StreakAtRisk
                    ? TelegramTexts.Get(c.Lang, "reminder_risk", day.Streak, day.TodayWords, day.GoalWords, left)
                    : day.TodayWords == 0
                        ? TelegramTexts.Get(c.Lang, "reminder_start", day.GoalWords)
                        : TelegramTexts.Get(c.Lang, "reminder_left", day.TodayWords, day.GoalWords, left);

                var result = await telegram.SendAsync(c.ChatId!.Value, text, PlayKeyboard(c.Lang, quiz: true));
                if (result == SendResult.Sent) { c.LastReminderAt = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
                else if (result == SendResult.Blocked) { db.TelegramConnections.Remove(c); await db.SaveChangesAsync(ct); }
                await Task.Delay(60, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning("Telegram pupil reminder failed: {Type}", ex.GetType().Name);
            }
        }
    }

    private static object PlayKeyboard(string lang, bool quiz)
    {
        var row = new List<object> { new { text = TelegramTexts.Get(lang, "play_btn"), callback_data = "play" } };
        if (quiz) row.Add(new { text = TelegramTexts.Get(lang, "quiz_btn"), callback_data = "quiz" });
        return new { inline_keyboard = new[] { row.ToArray() } };
    }

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}

// The few fields of a Telegram callback the pupil handlers need.
public readonly record struct CallbackInfo(string CallbackId, long ChatId, long MessageId);
