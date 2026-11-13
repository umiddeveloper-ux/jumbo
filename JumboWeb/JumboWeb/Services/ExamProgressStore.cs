using System.Text.Json;
using Jumbo.Shared.Dtos;
using Microsoft.JSInterop;

namespace JumboWeb.Services;

/// <summary>An exam in progress: everything that is needed to go on after F5 (the same words, the answers so far, the place, what is typed).</summary>
public class ExamSession
{
    public int UserId { get; set; }
    public int Book { get; set; }
    public int Count { get; set; }
    public string Kind { get; set; } = "type";           // type (the translation is typed) | test (one of four answers)
    public int? TeacherExamId { get; set; }
    public DateTime SavedUtc { get; set; }
    public List<ExamWordItem> Words { get; set; } = new();
    public List<ExamAnswer> Answers { get; set; } = new();
    public int BatchStart { get; set; }
    public List<string> Inputs { get; set; } = new();
    public List<List<string>> Options { get; set; } = new();          // test: the four answers of every question (kept in order)
}

/// <summary>The result of the last exam, kept for F5 on the page of results.</summary>
public class ExamResultSnapshot
{
    public int UserId { get; set; }
    public List<ExamAnswer> Answers { get; set; } = new();
    public int Book { get; set; }
    public int Count { get; set; }
    public string Kind { get; set; } = "type";
    public int? TeacherExamId { get; set; }
    public bool Sent { get; set; }
}

/// <summary>The best result of a free exam in one book and one kind of exam (typing / test).</summary>
public class ExamBestEntry
{
    public int Correct { get; set; }
    public int Total { get; set; }
    public DateTime Utc { get; set; }
    public int Percent => Total > 0 ? (int)Math.Round(Correct * 100.0 / Total) : 0;
}

/// <summary>
/// Keeps the exam of the learner in the localStorage of the browser: pressing F5 (or closing and opening the tab) does not throw the exam away.
/// One exam per browser; it is used only by the same learner, for the same book / number / kind / teacher's code, and for 8 hours.
/// A failure of the storage (private mode) is never an error: the exam just does not survive F5 then.
/// </summary>
public class ExamProgressStore(IJSRuntime js)
{
    private const string SessionKey = "lp-exam-session";
    private const string ResultKey = "lp-exam-result";
    private const string BestKey = "lp-exam-best";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(8);

    public async Task SaveSessionAsync(ExamSession s)
    {
        s.SavedUtc = DateTime.UtcNow;
        await SetAsync(SessionKey, JsonSerializer.Serialize(s));
    }

    public async Task<ExamSession?> LoadSessionAsync(int userId, int book, int count, string kind, int? teacherExamId)
    {
        var s = Parse<ExamSession>(await GetAsync(SessionKey));
        if (s is null || s.Words.Count == 0) return null;
        if (s.UserId != userId || s.Book != book || s.Count != count || s.Kind != kind || s.TeacherExamId != teacherExamId) return null;
        if (DateTime.UtcNow - s.SavedUtc > Ttl) { await ClearSessionAsync(); return null; }
        return s;
    }

    public Task ClearSessionAsync() => RemoveAsync(SessionKey);

    public Task SaveResultAsync(ExamResultSnapshot r) => SetAsync(ResultKey, JsonSerializer.Serialize(r));

    public async Task<ExamResultSnapshot?> LoadResultAsync(int userId)
    {
        var r = Parse<ExamResultSnapshot>(await GetAsync(ResultKey));
        return r is not null && r.UserId == userId && r.Answers.Count > 0 ? r : null;
    }

    // ---- the best results of the free exams (a free exam is not stored on the server): per learner, book and kind of exam ----
    private static string BestId(int userId, int book, string kind) => $"{userId}:{book}:{kind}";

    public async Task<Dictionary<string, ExamBestEntry>> LoadBestAsync() =>
        Parse<Dictionary<string, ExamBestEntry>>(await GetAsync(BestKey)) ?? new();

    /// <summary>Remembers the result if it is the best so far for this book and kind (the same percent: the newer one stays).</summary>
    public async Task SaveBestAsync(int userId, int book, string kind, int correct, int total)
    {
        if (userId <= 0 || total <= 0) return;
        var all = await LoadBestAsync();
        var fresh = new ExamBestEntry { Correct = correct, Total = total, Utc = DateTime.UtcNow };
        var id = BestId(userId, book, kind);
        if (all.TryGetValue(id, out var old) && old.Percent > fresh.Percent) return;
        all[id] = fresh;
        await SetAsync(BestKey, JsonSerializer.Serialize(all));
    }

    public static ExamBestEntry? BestFor(Dictionary<string, ExamBestEntry> all, int userId, int book, string kind) =>
        all.GetValueOrDefault(BestId(userId, book, kind));

    private static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json); } catch { return null; }
    }

    private async Task<string?> GetAsync(string key) { try { return await js.InvokeAsync<string?>("lp.store.get", key); } catch { return null; } }
    private async Task SetAsync(string key, string value) { try { await js.InvokeVoidAsync("lp.store.set", key, value); } catch { } }
    private async Task RemoveAsync(string key) { try { await js.InvokeVoidAsync("lp.store.remove", key); } catch { } }
}
