using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Jumbo.Api.Services;

/// <summary>
/// Reads and changes a learner's lives and LP on the server (the rules are in <see cref="LivesRules"/>). The learner's row is locked while it
/// is changed, so the site and the app working at once cannot overwrite each other.
/// LP for a won game are given here too, by the game, with two limits that make forging a win in a loop pointless: not more often than every
/// <see cref="MinSecondsBetweenAwards"/> seconds and not more than <see cref="DailyAwardLimit"/> LP a day (a game takes a minute; a diligent
/// learner earns a few hundred LP a day). The count is in memory: a restart of the API forgets it, which is acceptable for a limit.
/// </summary>
public class LivesService(AppDbContext db, IMemoryCache cache)
{
    public const int MinSecondsBetweenAwards = 10;
    public const int DailyAwardLimit = 1000;

    public async Task<LivesStateDto> GetAsync(int userId)
    {
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await LockAsync(userId);
        if (LivesRules.Regen(user, now)) await db.SaveChangesAsync();
        await tx.CommitAsync();
        return LivesRules.State(user, now, LivesRules.CanPlay(user, livesOnly: false));
    }

    public async Task<LivesStateDto> MistakeAsync(int userId, bool livesOnly)
    {
        var now = DateTime.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await LockAsync(userId);
        bool canPlay = LivesRules.Mistake(user, livesOnly, now);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return LivesRules.State(user, now, canPlay);
    }

    /// <summary>The LP for a won game (0 when the game gives none, or a limit is reached).</summary>
    public async Task<LivesStateDto> AwardGameWinAsync(int userId, string gameType)
    {
        var now = DateTime.UtcNow;
        int reward = LivesRules.GameRewards.GetValueOrDefault(gameType);
        int awarded = reward > 0 && TryTakeAwardSlot(userId, reward, now) ? reward : 0;

        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await LockAsync(userId);
        LivesRules.Regen(user, now);
        user.LpPoints += awarded;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return LivesRules.State(user, now, LivesRules.CanPlay(user, livesOnly: false), awarded);
    }

    private bool TryTakeAwardSlot(int userId, int reward, DateTime now)
    {
        var lastKey = $"lp-last:{userId}";
        if (cache.TryGetValue(lastKey, out DateTime last) && (now - last).TotalSeconds < MinSecondsBetweenAwards) return false;

        var dayKey = $"lp-day:{userId}:{now.AddHours(5):yyyyMMdd}";          // the day of the app is UTC+5
        cache.TryGetValue(dayKey, out int today);
        if (today + reward > DailyAwardLimit) return false;

        cache.Set(lastKey, now, TimeSpan.FromMinutes(1));
        cache.Set(dayKey, today + reward, TimeSpan.FromHours(30));
        return true;
    }

    // the row of the learner, locked until the end of the transaction
    private async Task<User> LockAsync(int userId) =>
        await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE user_id = {userId} FOR UPDATE").SingleAsync();
}
