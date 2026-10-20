using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

// Daily goal + streak. A "day" is the app-time day (Uzbekistan, UTC+5, no DST) so that the
// website and the mobile app agree on when a day rolls over regardless of device settings.
public class DailyProgressService(AppDbContext db)
{
    public static readonly int[] AllowedGoals = [5, 10, 20];

    private static readonly TimeSpan AppUtcOffset = TimeSpan.FromHours(5);

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow + AppUtcOffset);

    // Records words practised today (distinct per day) and updates the streak when the goal is reached.
    public async Task<DailyProgressDto> RecordWordsAsync(int userId, IEnumerable<int> wordIds)
    {
        var today = Today;
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        ApplyFreeze(user);

        // Rows from previous days are only needed to dedupe within a day.
        await db.UserDailyWords.Where(w => w.UserId == userId && w.Date < today).ExecuteDeleteAsync();

        var already = await db.UserDailyWords
            .Where(w => w.UserId == userId && w.Date == today)
            .Select(w => w.WordId)
            .ToListAsync();
        var fresh = wordIds.Distinct().Except(already).ToList();

        var progress = await GetOrCreateDayAsync(userId, today);
        if (fresh.Count > 0)
        {
            foreach (var id in fresh)
                db.UserDailyWords.Add(new UserDailyWord { UserId = userId, Date = today, WordId = id });
            progress.WordsCount += fresh.Count;
        }

        ApplyGoal(user, progress, today);
        await db.SaveChangesAsync();
        return await BuildAsync(user);
    }

    // Changing the goal can instantly complete (or not) today's goal.
    public async Task<DailyProgressDto> SetGoalAsync(int userId, int goalWords)
    {
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        user.DailyGoalWords = goalWords;
        ApplyFreeze(user);

        var today = Today;
        var progress = await db.UserDailyProgresses.SingleOrDefaultAsync(p => p.UserId == userId && p.Date == today);
        if (progress is not null) ApplyGoal(user, progress, today);

        await db.SaveChangesAsync();
        return await BuildAsync(user);
    }

    public async Task<DailyProgressDto> GetAsync(int userId)
    {
        var user = await db.Users.SingleAsync(u => u.UserId == userId);
        if (ApplyFreeze(user)) await db.SaveChangesAsync();
        return await BuildAsync(user);
    }

    private async Task<UserDailyProgress> GetOrCreateDayAsync(int userId, DateOnly date)
    {
        var row = await db.UserDailyProgresses.SingleOrDefaultAsync(p => p.UserId == userId && p.Date == date);
        if (row is null)
        {
            row = new UserDailyProgress { UserId = userId, Date = date };
            db.UserDailyProgresses.Add(row);
        }
        return row;
    }

    // One day was missed and the learner has a freeze: it is used up and the streak goes on as if yesterday had been a goal day.
    private static bool ApplyFreeze(User user)
    {
        if (user.FreezeCount <= 0 || user.Streak <= 0 || user.LastGoalDate is not { } last || last != Today.AddDays(-2)) return false;
        user.FreezeCount--;
        user.LastGoalDate = Today.AddDays(-1);
        return true;
    }

    private static void ApplyGoal(User user, UserDailyProgress progress, DateOnly today)
    {
        if (progress.GoalMet || progress.WordsCount < user.DailyGoalWords) return;

        progress.GoalMet = true;
        user.Streak = user.LastGoalDate == today.AddDays(-1) ? user.Streak + 1 : 1;
        user.LastGoalDate = today;
    }

    private async Task<DailyProgressDto> BuildAsync(User user)
    {
        var today = Today;
        var from = today.AddDays(-6);
        var rows = await db.UserDailyProgresses.AsNoTracking()
            .Where(p => p.UserId == user.UserId && p.Date >= from && p.Date <= today)
            .ToDictionaryAsync(p => p.Date);

        var week = Enumerable.Range(0, 7)
            .Select(i => from.AddDays(i))
            .Select(d => rows.TryGetValue(d, out var r)
                ? new DailyDayDto { Date = d, Words = r.WordsCount, GoalMet = r.GoalMet }
                : new DailyDayDto { Date = d })
            .ToList();

        var todayRow = week[^1];
        var streakAlive = user.LastGoalDate is { } last && last >= today.AddDays(-1);
        var streak = streakAlive ? user.Streak : 0;

        return new DailyProgressDto
        {
            GoalWords = user.DailyGoalWords,
            TodayWords = todayRow.Words,
            GoalMetToday = todayRow.GoalMet,
            Streak = streak,
            FreezeCount = user.FreezeCount,
            StreakAtRisk = streak > 0 && !todayRow.GoalMet,
            Week = week
        };
    }
}
