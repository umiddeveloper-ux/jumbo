using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Controls;
using Jumbo.Services;
using Jumbo.Shared;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>A book chip of the horizontal book picker.</summary>
public partial class BookChip : ObservableObject
{
    public int Number { get; init; }
    public int Progress { get; init; }                 // 0..100
    public bool IsLocked { get; init; }                // the check over the previous book is not passed yet
    public bool IsZero => Number == 0;                 // Book 0: the alphabet and the numbers, always open, not a part of the course
    public bool IsDone => Progress >= 100;
    public string Label => IsZero ? "ABC" : IsDone ? "✓" : Number.ToString();   // book 0 reads ABC with 123 under it

    [ObservableProperty]
    private bool _isSelected;
}

public partial class DashboardPageModel : ObservableObject
{
    private bool _dataLoaded;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private readonly LivesService _livesService;
    private readonly BookGateService _gate;
    private HashSet<int> _unlocked = [1];
    private readonly AppUpdateService _update;
    private int _pendingOpen;
    private static bool _startHandled;      // the automatic opening of the topics happens once per start of the app

    private readonly Dictionary<int, List<Subject>> _subjectsByBook = new();
    private Dictionary<int, UserSubjectProgress> _progress = new();
    private Subject? _currentOverall;                   // first not fully won topic of all books

    // ── daily goal ──
    [ObservableProperty]
    private DailyProgressDto? _daily;

    // ── rating tile (the level is in the title bar) ──
    [ObservableProperty]
    private string _ratingPlaceText = string.Empty;

    [ObservableProperty]
    private string _ratingOutOfText = string.Empty;

    [ObservableProperty]
    private string _ratingSubText = string.Empty;

    private RatingSummary? _rating;
    private bool _ratingLoaded;

    // ── continue card ──
    [ObservableProperty]
    private bool _hasContinue;

    [ObservableProperty]
    private bool _isAllDone;

    [ObservableProperty]
    private string _continueLabel = string.Empty;

    [ObservableProperty]
    private string _continueTopic = string.Empty;

    // ── books ──
    [ObservableProperty]
    private ObservableCollection<BookChip> _books = [];

    [ObservableProperty]
    private int _selectedBookNumber = 1;

    // ── alphabet + overall progress ──
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AlphabetSummaryColor))]
    private List<LetterItem> _alphabet = [];

    [ObservableProperty]
    private double _totalProgress;

    [ObservableProperty]
    private string _wordsProgressLabel = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    // Цвет кнопки "Алфавит" отражает самый частый уровень повторения среди изученных букв.
    public Color AlphabetSummaryColor
    {
        get
        {
            var withWords = Alphabet.Where(l => l.HasWords).ToList();
            if (withWords.Count == 0) return RepetitionLevelToColorConverter.GetColor(-1);

            int dominantLevel = withWords
                .GroupBy(l => l.RepetitionLevel)
                .OrderByDescending(g => g.Count())
                .First().Key;

            return RepetitionLevelToColorConverter.GetColor(dominantLevel);
        }
    }

    public DashboardPageModel(
        LpApiClient apiClient,
        LivesService livesService,
        ModalErrorHandler errorHandler,
        BookGateService gate,
        AppUpdateService update)
    {
        _gate = gate;
        _update = update;
        _apiClient = apiClient;
        _livesService = livesService;
        _errorHandler = errorHandler;

        // Texts that were built in code follow the language switch.
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RebuildTexts);
    }

    // ── loading ─────────────────────────────────────────────────────────────
    private async Task LoadData(User? knownUser = null)
    {
        try
        {
            IsBusy = !_dataLoaded;

            // Everything the page needs is asked at the same time: one wait of the network instead of ten one after another
            // (the topics of the books come from the cache after the first time).
            var dailyTask = Safe(() => _apiClient.GetDailyProgressAsync());
            var ratingTask = Safe(async () => await MyRating.LoadAsync(_apiClient, knownUser ?? await _apiClient.GetCurrentUserAsync()));      // decorative: never break the page
            var alphabetTask = _apiClient.GetAlphabetProgressAsync(UserLanguagePrefs.StudyLangId);
            var unlockedTask = Safe(() => _apiClient.GetUnlockedBooksAsync());
            var progressTask = _apiClient.GetUserSubjectProgressAsync();
            var wordsTask = _apiClient.GetUserWordsAsync();
            var totalTask = _apiClient.GetWordsCountAsync(UserLanguagePrefs.StudyLangId);

            var bookNums = Enumerable.Range(1, Jumbo.Shared.Course.BookCount).ToList();      // (no wait for the list of books: the topics are asked at once)
            var subjectTasks = bookNums.Select(bn => _apiClient.GetSubjectsByBookAsync(bn)).ToList();
            await Task.WhenAll(dailyTask, ratingTask, alphabetTask, unlockedTask, progressTask, wordsTask, totalTask, Task.WhenAll(subjectTasks));

            Daily = await dailyTask;
            if (Daily is not null) _livesService.Streak = Daily.Streak;
            _rating = await ratingTask;
            _ratingLoaded = true;
            Alphabet = await alphabetTask;
            _unlocked = (await unlockedTask)?.ToHashSet() ?? [1];
            if (_unlocked.Count == 0) _unlocked = [1];
            _progress = (await progressTask).ToDictionary(p => p.SubjectId);

            _subjectsByBook.Clear();
            for (int i = 0; i < bookNums.Count; i++)
                _subjectsByBook[bookNums[i]] = (await subjectTasks[i]).OrderBy(s => s.SequenceNumber).ToList();

            // A book's bar moves with every won game: each of the 4 tracked games is 25% of a topic.
            var chips = new List<BookChip>();
            chips.Add(new BookChip { Number = 0 });            // Book 0 comes first
            for (int bn = 1; bn <= Jumbo.Shared.Course.BookCount; bn++)
            {
                int pct = 0;
                if (_subjectsByBook.TryGetValue(bn, out var subs) && subs.Count > 0)
                    pct = (int)(subs.Sum(s => WonFraction(s.SubjectId)) / subs.Count * 100);
                chips.Add(new BookChip { Number = bn, Progress = pct, IsLocked = !_unlocked.Contains(bn) });
            }

            // "Continue" and the header's book only look at the books the learner may open.
            _currentOverall = bookNums.Where(_unlocked.Contains).SelectMany(bn => _subjectsByBook[bn]).FirstOrDefault(s => PctOf(s) < 100);
            _livesService.CurrentBook = _currentOverall?.BookId ?? _unlocked.Max();
            if (_gate.PendingOpenBook > 0 && _unlocked.Contains(_gate.PendingOpenBook))
            {
                SelectedBookNumber = _pendingOpen = _gate.PendingOpenBook;       // the learner has just passed the check of this book
                _gate.PendingOpenBook = 0;
            }
            else if (!_dataLoaded) SelectedBookNumber = _currentOverall?.BookId ?? bookNums.FirstOrDefault(1);

            foreach (var c in chips) c.IsSelected = c.Number == SelectedBookNumber;
            Books = new ObservableCollection<BookChip>(chips);

            int studied = (await wordsTask).Count;
            int total = await totalTask;
            TotalProgress = total > 0 ? (double)studied / total : 0.0;
            WordsProgressLabel = $"{studied} / {total}";

            RebuildTexts();
            _ = _update.CheckAsync();       // "a new version is out" (at most every few hours; does not hold the page back)

            // the learner has just passed the check of a book: it opens on its own page
            if (_pendingOpen > 0) { var b = _pendingOpen; _pendingOpen = 0; _startHandled = true; await Shell.Current.GoToAsync($"BookPage?book={b}"); }
            else if (!_startHandled)
            {
                // Not the first time in the app (at least one topic is passed): the topics open at once, and the road scrolls to the topic to pass.
                _startHandled = true;
                if (_progress.Values.Any(p => p.AllWon) && _currentOverall is { } cur)
                {
                    await Task.Delay(250);
                    await Shell.Current.GoToAsync($"BookPage?book={cur.BookId}");
                }
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    // A request whose failure must not break the page: the result is empty then.
    private static async Task<T?> Safe<T>(Func<Task<T>> load)
    {
        try { return await load(); }
        catch { return default; }
    }

    private double WonFraction(int subjectId)
    {
        if (!_progress.TryGetValue(subjectId, out var p)) return 0;
        return p.PartsDone / (double)UserSubjectProgress.Parts;     // three games count: Find, Listen, Phrase
    }

    private int PctOf(Subject s) => PartsOf(s) * 100 / UserSubjectProgress.Parts;

    private int PartsOf(Subject s) => _progress.TryGetValue(s.SubjectId, out var p) ? p.PartsDone : 0;

    /// <summary>Texts that depend on the app language (rebuilt on load and on a language switch).</summary>
    private void RebuildTexts()
    {
        if (_rating is { } r)
        {
            RatingPlaceText = LocalizationManager.Format("Stats_Place", r.Rank);
            RatingOutOfText = LocalizationManager.Format("Stats_OutOf", r.Total);
            RatingSubText = $"{LocalizationManager.Get(r.ScopeKey)} · {LocalizationManager.Format("Stats_Words", r.Words)}";
        }
        else
        {
            RatingPlaceText = LocalizationManager.Get("Stats_RatingTitle");
            RatingOutOfText = string.Empty;
            RatingSubText = _ratingLoaded ? LocalizationManager.Get("Stats_NoPlace") : "…";
        }

        if (_currentOverall is { } cur)
        {
            HasContinue = true;
            IsAllDone = false;
            ContinueLabel = LocalizationManager.Get(PctOf(cur) > 0 ? "Path_Continue" : "Path_Start");
            ContinueTopic = $"{LocalizationManager.Format("Common_BookLabel", cur.BookId)} · {cur.SubjectName}";
        }
        else
        {
            HasContinue = false;
            IsAllDone = _subjectsByBook.Count > 0;
            ContinueLabel = string.Empty;
            ContinueTopic = LocalizationManager.Get("Path_AllDone");
        }

    }

    // ── interaction ─────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SelectBook(BookChip? chip)
    {
        if (chip is null) return;
        if (chip.IsZero)
        {
            await Shell.Current.GoToAsync("BookZeroPage");      // the alphabet and the numbers: nothing to unlock, nothing to count
            return;
        }
        if (chip.IsLocked)
        {
            // The book before it is open: the three-step check over that book opens this one. Otherwise: pass the earlier book first.
            if (chip.Number >= 2 && _unlocked.Contains(chip.Number - 1))
                await Shell.Current.GoToAsync($"BookGatePage?book={chip.Number}");
            else
                await Shell.Current.DisplayAlertAsync(
                    "🔒 " + LocalizationManager.Format("Gate_LockedTitle", chip.Number - 1),
                    LocalizationManager.Format("Gate_LockedText", chip.Number - 1),
                    LocalizationManager.Get("Gate_Close"));
            return;
        }
        // The book opens on its own page (the snake of its topics), the same as on the website.
        await Shell.Current.GoToAsync($"BookPage?book={chip.Number}");
    }

    // Opens the website's rating already signed in: the API hands out a one-time code, the site trades it for the session.
    [RelayCommand]
    private async Task OpenRatingOnWeb()
    {
        try
        {
            var code = await _apiClient.CreateWebLinkAsync();
            var url = code is { } c ? $"{ApiConfig.BaseUrl}go/{c}?next=/rating" : $"{ApiConfig.BaseUrl}rating";
            await Launcher.Default.OpenAsync(new Uri(url));
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    [RelayCommand]
    private async Task Continue()
    {
        if (_currentOverall is { } cur) await Shell.Current.GoToAsync($"SubjectDetailPage?subjectId={cur.SubjectId}");
    }

    [RelayCommand]
    private async Task OpenAlphabet() => await Shell.Current.GoToAsync("AlphabetPage");

    // ── lifecycle ───────────────────────────────────────────────────────────
    private async Task InitData()
    {
        var user = await _apiClient.GetCurrentUserAsync();
        if (user is not null)
        {
            UserLanguagePrefs.Save(user.AppLanguageId, user.StudyLanguageId, user.TranslationLanguageId);

            // The account language wins (it may have been changed on another device).
            var code = user.AppLanguageId switch { 1 => "en", 2 => "ru", 3 => "uz", _ => null };
            if (code is not null && code != LocalizationService.CurrentCode) LocalizationService.SetLanguage(code);
        }

        // the lives and the page are read at the same time, the learner (already read) is not asked a second and a third time
        await Task.WhenAll(_livesService.InitAsync(user), RefreshCore(user));
    }

    [RelayCommand]
    private Task Refresh() => RefreshCore(null);

    private async Task RefreshCore(User? knownUser)
    {
        try
        {
            IsRefreshing = true;
            await LoadData(knownUser);
            _dataLoaded = true;
        }
        catch (Exception e)
        {
            _errorHandler.HandleError(e);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void NavigatedTo() { }

    [RelayCommand]
    private void NavigatedFrom() { }

    [RelayCommand]
    private async Task Appearing()
    {
        // On cold start the Dashboard tab appears before AppShell's redirect-to-registration
        // check has run, so calling auth-required endpoints here would fail with 401. Skip
        // loading until the user is actually registered; Appearing fires again once Shell
        // navigates back to this tab after registration completes.
        if (!UserSession.IsRegistered) return;

        if (!_dataLoaded)
        {
            await Task.Delay(100);
            await InitData();
        }
        else
        {
            await Refresh();
        }
    }
}
