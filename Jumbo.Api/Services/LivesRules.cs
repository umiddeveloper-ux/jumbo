using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;

namespace Jumbo.Api.Services;

/// <summary>
/// The rules of lives and LP, in one place and on the server: the apps and the site used to count them themselves and send the result
/// (so anybody could write any number). Pure functions over a <see cref="User"/>; <see cref="LivesService"/> saves what they change.
/// </summary>
public static class LivesRules
{
    public const int MaxLives = 5;

    /// <summary>When the lives are gone and the LP too (or only the lives, in a check), they come back after this many hours.</summary>
    public const int BlockHours = 5;

    /// <summary>The LP a wrong answer costs when the lives are gone.</summary>
    public const int MistakeLpCost = 2;

    /// <summary>The LP for a won game, by the game (the apps show the same numbers: LpReward in the games).</summary>
    public static readonly IReadOnlyDictionary<string, int> GameRewards = new Dictionary<string, int>
    {
        ["FindMe"] = 5, ["Sequence"] = 7, ["ListenAndFind"] = 7, ["Steps"] = 10, ["Phrase"] = 8
    };

    public static bool Unlimited(User user, DateTime now) => Plans.NoLimits(Plans.Effective(user.Plan, user.PlanUntil, now));

    /// <summary>Brings the lives up to date: a paid version has them full; the block that is over gives them back. Returns true when something changed.</summary>
    public static bool Regen(User user, DateTime now)
    {
        if (Unlimited(user, now))
        {
            if (user.LifeCount == MaxLives && user.LifeExpirationDate == default) return false;
            user.LifeCount = MaxLives;
            user.LifeExpirationDate = default;
            return true;
        }

        // no lives and no timer (old data): the block starts now, otherwise the lives would never come back
        if (user.LifeCount <= 0 && user.LifeExpirationDate == default)
        {
            user.LifeExpirationDate = now.AddHours(BlockHours);
            return true;
        }

        if (user.LifeCount < MaxLives && user.LifeExpirationDate != default && now >= user.LifeExpirationDate)
        {
            user.LifeCount = MaxLives;               // the block is over: the lives are full again (the LP stay as they are)
            user.LifeExpirationDate = default;
            return true;
        }
        return false;
    }

    /// <summary>
    /// A wrong answer. A shield takes it; otherwise a life goes, and when there are none the LP go (not in a check, livesOnly).
    /// When both are gone the block of 5 hours starts. Returns whether the learner can go on.
    /// </summary>
    public static bool Mistake(User user, bool livesOnly, DateTime now)
    {
        if (Unlimited(user, now)) { Regen(user, now); return true; }

        Regen(user, now);
        bool wasEmpty = user.LifeCount <= 0 && user.LpPoints <= 0;

        if (user.ShieldCount > 0 && (user.LifeCount > 0 || (!livesOnly && user.LpPoints > 0)))
        {
            user.ShieldCount--;
            return CanPlay(user, livesOnly);
        }

        if (user.LifeCount > 0)
        {
            user.LifeCount--;
            if (user.LifeCount == 0 && user.LifeExpirationDate == default) user.LifeExpirationDate = now.AddHours(BlockHours);
        }
        else if (!livesOnly && user.LpPoints > 0)
        {
            user.LpPoints = Math.Max(0, user.LpPoints - MistakeLpCost);
        }

        // both are gone: the block of 5 hours starts now
        if (!wasEmpty && user.LifeCount <= 0 && user.LpPoints <= 0) user.LifeExpirationDate = now.AddHours(BlockHours);
        return CanPlay(user, livesOnly);
    }

    public static bool CanPlay(User user, bool livesOnly) => livesOnly ? user.LifeCount > 0 : user.LifeCount > 0 || user.LpPoints > 0;

    public static LivesStateDto State(User user, DateTime now, bool canPlay, int lpAwarded = 0) => new()
    {
        Lives = user.LifeCount,
        LpPoints = user.LpPoints,
        LifeRegenAt = user.LifeExpirationDate == default ? null : DateTime.SpecifyKind(user.LifeExpirationDate, DateTimeKind.Utc),
        ShieldCount = user.ShieldCount,
        Unlimited = Unlimited(user, now),
        CanPlay = canPlay,
        LpAwarded = lpAwarded
    };
}
