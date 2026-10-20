using System.Collections.Concurrent;

namespace Jumbo.ApiClient;

/// <summary>
/// A memory cache for the answers of the server that do not depend on the learner and hardly ever change: the topics of a book, the words
/// of a topic, a translation, a picture. A child who opens the same page a second time gets it at once, without waiting for the network.
/// The same request asked twice at the same moment is sent once. A failed request is never kept. The cache lives as long as the process
/// (the app, or the web server: the answers are the same for everybody there too).
/// </summary>
internal static class ApiCache
{
    /// <summary>Lists and single items (topics, words): they change only when a book is loaded again.</summary>
    public static readonly TimeSpan Normal = TimeSpan.FromMinutes(30);

    /// <summary>Pictures and translations of a word: they never change on their own.</summary>
    public static readonly TimeSpan Long = TimeSpan.FromHours(6);

    private const int MaxEntries = 8000;
    private static readonly ConcurrentDictionary<string, (Task Task, DateTime Expires)> Items = new();

    public static Task<T> GetAsync<T>(string key, TimeSpan ttl, Func<Task<T>> load)
    {
        var now = DateTime.UtcNow;
        if (Items.TryGetValue(key, out var entry) && entry.Expires > now && entry.Task is Task<T> cached
            && !cached.IsFaulted && !cached.IsCanceled)
            return cached;

        if (Items.Count >= MaxEntries) Items.Clear();       // a very long session: start again, nothing is lost but time
        var task = load();
        Items[key] = (task, now + ttl);
        _ = task.ContinueWith(t => Items.TryRemove(key, out _), TaskContinuationOptions.NotOnRanToCompletion);
        return task;
    }

    public static bool Contains(string key) => Items.TryGetValue(key, out var e) && e.Expires > DateTime.UtcNow && !e.Task.IsFaulted && !e.Task.IsCanceled;

    /// <summary>Puts an answer that is already known in the cache (e.g. the parts of a bigger answer).</summary>
    public static void Seed<T>(string key, TimeSpan ttl, T value) =>
        Items[key] = (Task.FromResult(value), DateTime.UtcNow + ttl);

    /// <summary>For a list: every caller gets its own copy (a page may sort or change the list it got).</summary>
    public static async Task<List<T>> GetListAsync<T>(string key, TimeSpan ttl, Func<Task<List<T>>> load) =>
        new List<T>(await GetAsync(key, ttl, load));

    /// <summary>Forget everything (e.g. after a sign-out).</summary>
    public static void Clear() => Items.Clear();
}
