using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Controls;
using Jumbo.Shared;
using Jumbo.Utilities;

namespace Jumbo.PageModels;

/// <summary>One topic of the open book and its state.</summary>
public class TopicRow
{
    public int Index { get; init; }
    public Subject Subject { get; init; } = null!;
    public string Name => Subject.SubjectName;
    public string State { get; init; } = "future";      // done | current | started | future
    public int Pct { get; init; }
    public int Parts { get; init; }                     // 0..3: how many of the three counted games are won
}

/// <summary>
/// The page of one book: the snake of its topics (the same page as the website's /little-polyglot/book/N). Tapping a book on the home
/// page opens it; a topic opens when the topic before it is passed.
/// </summary>
[QueryProperty(nameof(BookNumber), "book")]
public partial class BookPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private List<TopicRow> _topicRows = [];
    private bool _dataLoaded;

    /// <summary>The page redraws the snake when this fires (the topics, the text of the bubble over the topic to play next).</summary>
    public event Action<IReadOnlyList<SnakeNode>, string>? PathChanged;

    /// <summary>Raised once the road has been loaded: the index of the topic the learner has to pass (the page scrolls to it).</summary>
    public event Action<int>? FocusCurrent;

    [ObservableProperty] private int _bookNumber;
    [ObservableProperty] private string _mapBookLabel = string.Empty;
    [ObservableProperty] private string _levelText = string.Empty;
    [ObservableProperty] private string _topicsCountText = string.Empty;
    [ObservableProperty] private double _pathProgress;
    [ObservableProperty] private bool _hasNoTopics;
    [ObservableProperty] private bool _isBusy;

    public BookPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RebuildTexts);
    }

    // What each learner had on this page the last time (the key has the login token and the book): shown at once when the page is opened again.
    private static readonly Dictionary<string, Dictionary<int, UserSubjectProgress>> LastProgress = new();
    private string LastKey => (UserSession.AccessToken ?? string.Empty) + ":" + BookNumber;

    private async Task LoadAsync()
    {
        try
        {
            if (BookNumber <= 0) return;

            // The topics come from the cache of the client; the progress of the last visit is shown at once, the network only brings the news.
            var subjectsTask = _apiClient.GetSubjectsByBookAsync(BookNumber);
            var unlockedTask = Safe(() => _apiClient.GetUnlockedBooksAsync());
            var progressTask = _apiClient.GetUserSubjectProgressAsync();       // at the same time

            Dictionary<int, UserSubjectProgress>? last;
            lock (LastProgress) LastProgress.TryGetValue(LastKey, out last);
            IsBusy = !_dataLoaded && last is null;
            if (last is not null && !_dataLoaded)
            {
                Show((await subjectsTask).OrderBy(x => x.SequenceNumber).ToList(), last);
                IsBusy = false;
            }

            // A locked book has no page (the same rule as the website): back to the home page.
            var unlocked = (await unlockedTask)?.ToHashSet() ?? [1];
            if (!unlocked.Contains(BookNumber))
            {
                await Shell.Current.GoToAsync("..");
                return;
            }

            var progress = (await progressTask).ToDictionary(p => p.SubjectId);
            lock (LastProgress) LastProgress[LastKey] = progress;
            Show((await subjectsTask).OrderBy(x => x.SequenceNumber).ToList(), progress);
            _dataLoaded = true;
            int cur = _topicRows.FindIndex(r => r.State == "current");
            if (cur > 0) FocusCurrent?.Invoke(cur);          // topic 1 is on the screen already
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private static async Task<T?> Safe<T>(Func<Task<T>> load)
    {
        try { return await load(); }
        catch { return default; }
    }

    // The rows of the snake from the topics and the progress.
    private void Show(List<Subject> subs, Dictionary<int, UserSubjectProgress> progress)
    {
        int PartsOf(Subject s) => progress.TryGetValue(s.SubjectId, out var p) ? p.PartsDone : 0;
        int PctOf(Subject s) => PartsOf(s) * 100 / UserSubjectProgress.Parts;
        var current = subs.FirstOrDefault(s => PctOf(s) < 100);

        // done = all games won; current = first unfinished topic of this book; started = partly played; future = nothing won yet.
        _topicRows = subs.Select((sub, i) => new TopicRow
        {
            Index = i,
            Subject = sub,
            State = PctOf(sub) >= 100 ? "done" : current is not null && current.SubjectId == sub.SubjectId ? "current" : PctOf(sub) > 0 ? "started" : "future",
            Pct = PctOf(sub),
            Parts = PartsOf(sub)
        }).ToList();

        HasNoTopics = subs.Count == 0;
        PathProgress = _topicRows.Count == 0 ? 0 : _topicRows.Count(r => r.Pct >= 100) / (double)_topicRows.Count;
        RebuildTexts();
        _ = LoadIconsAsync();
    }

    /// <summary>Texts that depend on the app language (rebuilt on load and on a language switch).</summary>
    private void RebuildTexts()
    {
        MapBookLabel = LocalizationManager.Format("Common_BookLabel", BookNumber);
        LevelText = LanguageLevels.ForBook(BookNumber);
        TopicsCountText = LocalizationManager.Format("BookPage_Passed", _topicRows.Count(r => r.Pct >= 100), _topicRows.Count);
        PublishPath();
    }

    // The pictures of the topics that are not passed: asked after the road is shown (from the cache of the client when they were asked before).
    private readonly Dictionary<int, string> _svgs = new();

    private async Task LoadIconsAsync()
    {
        var todo = _topicRows.Where(r => r.Pct < 100 && r.Subject.SvgImageId > 0 && !_svgs.ContainsKey(r.Subject.SvgImageId)).Select(r => r.Subject.SvgImageId).Distinct().ToList();
        if (todo.Count == 0) return;
        using var gate = new SemaphoreSlim(6);
        await Task.WhenAll(todo.Select(async id =>
        {
            await gate.WaitAsync();
            try { var svg = await _apiClient.GetSvgByIdAsync(id); if (!string.IsNullOrEmpty(svg)) lock (_svgs) _svgs[id] = svg; }
            catch { /* the number stays in the circle */ }
            finally { gate.Release(); }
        }));
        MainThread.BeginInvokeOnMainThread(PublishPath);
    }

    private void PublishPath()
    {
        // a topic opens when the topic before it is passed: the ones after the first topic that is not passed are locked
        int open = _topicRows.FindIndex(r => r.Pct < 100);
        var nodes = _topicRows.Select(r => new SnakeNode(r.Name, r.State, r.Index + 1, r.Parts, open >= 0 && r.Pct < 100 && r.Index > open,
            _svgs.GetValueOrDefault(r.Subject.SvgImageId))).ToList();
        var current = _topicRows.FirstOrDefault(r => r.State == "current");
        var bubble = current is null ? string.Empty : LocalizationManager.Get(current.Pct > 0 ? "Path_Continue" : "Path_Start");
        PathChanged?.Invoke(nodes, bubble);
    }

    [RelayCommand]
    private async Task SelectNode(int index)
    {
        if (index < 0 || index >= _topicRows.Count) return;
        var row = _topicRows[index];

        // The topic before it is not passed: say so, with "Got it" and "Go" (to the topic to pass).
        int open = _topicRows.FindIndex(r => r.Pct < 100);
        if (open >= 0 && row.Pct < 100 && row.Index > open)
        {
            var cur = _topicRows[open];
            bool go = await Shell.Current.DisplayAlertAsync(
                "🔒 " + LocalizationManager.Get("Topic_LockedTitle"),
                LocalizationManager.Format("Topic_LockedText", cur.Name),
                LocalizationManager.Get("Topic_Go"),
                LocalizationManager.Get("Topic_Understood"));
            if (go) await Shell.Current.GoToAsync($"SubjectDetailPage?subjectId={cur.Subject.SubjectId}");
            return;
        }
        await Shell.Current.GoToAsync($"SubjectDetailPage?subjectId={row.Subject.SubjectId}");
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (!UserSession.IsRegistered) return;
        await LoadAsync();
    }
}
