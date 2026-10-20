using System.Collections.Concurrent;

namespace Jumbo.Api.Services;

/// <summary>
/// Protection against guessing a password. Failed attempts are counted per account (the login the attempt is for, or the id of the learner
/// who is changing his password): 5 failures within 15 minutes lock the account for 5 minutes; a lock that comes again within a day is
/// longer (15 minutes, then an hour). While an account is locked even the right password is not tried. A right password clears the count.
/// The count is per account, not per address, because the website reaches the API from its own container: every visitor would have the
/// same address. In memory: a restart of the API forgets it, which is acceptable for a lock of minutes.
/// </summary>
public class LoginThrottle
{
    public const int MaxFailures = 5;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan[] Locks = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1)];
    private static readonly TimeSpan LevelMemory = TimeSpan.FromDays(1);
    private const int MaxEntries = 50_000;           // random logins must not fill the memory

    private sealed class State
    {
        public int Failures;
        public DateTime WindowStart;
        public DateTime LockedUntil;
        public int Level;
        public DateTime LastFailure;
    }

    private readonly ConcurrentDictionary<string, State> _states = new();
    private readonly Func<DateTime> _now;

    public LoginThrottle() : this(() => DateTime.UtcNow) { }

    public LoginThrottle(Func<DateTime> now) => _now = now;

    /// <summary>The key of an attempt to log in as this login (case does not matter).</summary>
    public static string ForLogin(string? username) => "login:" + Normalize(username);

    /// <summary>The key of the password checks of a signed-in learner (changing the login or the password, deleting the account).</summary>
    public static string ForUser(int userId) => "user:" + userId;

    private static string Normalize(string? s)
    {
        var t = (s ?? string.Empty).Trim().ToLowerInvariant();
        return t.Length > 64 ? t[..64] : t;
    }

    /// <summary>How long the account is still locked; null when it is not.</summary>
    public TimeSpan? Blocked(string key)
    {
        if (!_states.TryGetValue(key, out var s)) return null;
        lock (s)
        {
            var left = s.LockedUntil - _now();
            return left > TimeSpan.Zero ? left : null;
        }
    }

    /// <summary>A failed attempt. Returns how long the account is locked now (the fifth failure starts the lock), or null.</summary>
    public TimeSpan? Fail(string key)
    {
        var now = _now();
        if (_states.Count > MaxEntries) Prune(now);
        if (_states.Count > MaxEntries && !_states.ContainsKey(key)) return null;       // full: count nothing new, the answer to the user stays the same

        var s = _states.GetOrAdd(key, _ => new State { WindowStart = now });
        lock (s)
        {
            if (s.LockedUntil > now) return s.LockedUntil - now;                     // already locked (a parallel attempt)
            if (now - s.WindowStart > Window) { s.Failures = 0; s.WindowStart = now; }
            if (now - s.LastFailure > LevelMemory) s.Level = 0;                      // a quiet day: the long locks are forgotten
            s.LastFailure = now;

            if (++s.Failures < MaxFailures) return null;
            var lockFor = Locks[Math.Min(s.Level, Locks.Length - 1)];
            s.Level++;
            s.Failures = 0;
            s.WindowStart = now;
            s.LockedUntil = now + lockFor;
            return lockFor;
        }
    }

    /// <summary>The right password: the failures are forgotten (the level of the locks stays until a quiet day).</summary>
    public void Success(string key)
    {
        if (!_states.TryGetValue(key, out var s)) return;
        lock (s) { s.Failures = 0; s.WindowStart = _now(); }
    }

    private void Prune(DateTime now)
    {
        foreach (var (key, s) in _states)
            lock (s)
                if (s.LockedUntil <= now && now - s.LastFailure > LevelMemory) _states.TryRemove(key, out _);
    }

    /// <summary>Whole minutes to wait, at least one, for the message to the user.</summary>
    public static int Minutes(TimeSpan left) => Math.Max(1, (int)Math.Ceiling(left.TotalMinutes));
}
