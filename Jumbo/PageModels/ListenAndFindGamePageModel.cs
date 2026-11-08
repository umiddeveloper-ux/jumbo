using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using Microsoft.Maui.Media;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(SubjectId), "subjectId")]
[QueryProperty(nameof(GateBook), "gate")]
public partial class ListenAndFindGamePageModel : ObservableObject
{
    private const int TotalSteps = 15;   // one step for each of the 15 words of a topic
    private const int LpReward = 7;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private readonly BookGateService _gate;

    public LivesService LivesService { get; }

    /// <summary>Jumbo the coach: cheers right answers, comforts after wrong ones (bind to a TigerCoach).</summary>
    public GameCoach Coach { get; } = new();

    public string RewardText => GateBook > 0 ? string.Empty : LocalizationManager.Format("Common_LpEarned", LpReward);

    private record WordData(int WordId, int SvgImageId, string EnglishWord, string TranslationWord, string SvgHtml, int Number = 0, string Level = "");

    private List<WordData> _allWords = [];
    private List<WordData> _stepTargets = [];
    private int _currentStepIndex = -1;
    private int _currentUserId;
    private WordData? _currentTarget;
    private CancellationTokenSource? _ttsCts;

    [ObservableProperty] private int _subjectId;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isInteractive;
    [ObservableProperty] private bool _isDeafMode;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private bool _hasNoData;

    [ObservableProperty] private string _targetWordText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressValue), nameof(ProgressText))]
    private int _step = 0;

    public double ProgressValue => (double)_step / TotalSteps;
    public string ProgressText => $"{_step} / {TotalSteps}";

    public ObservableCollection<ListenCardViewModel> AllCards { get; } = [];

    // No lives and no LP: the 5-hour block (the lock panel with the clock). In the check that opens a book only the lives count.
    public bool IsLocked => LivesService.IsBlocked || (GateBook > 0 && LivesService.Lives <= 0);
    private bool _wasLocked;
    private void OnLivesChanged()
    {
        OnPropertyChanged(nameof(IsLocked));
        if (IsLocked) _wasLocked = true;
        else if (_wasLocked) { _wasLocked = false; _ = LoadGameAsync(); }     // the block is over: the game starts
    }

    // A mistake costs a life; in the check only the lives count, and without them the check is failed (its steps start again).
    private async Task<bool> HitAsync()
    {
        var ok = await LivesService.TakeDamageAsync(livesOnly: GateBook > 0);
        if (!ok && GateBook > 0) _gate.Reset(GateBook);
        return ok;
    }

    public ListenAndFindGamePageModel(
        LpApiClient apiClient,
        LivesService livesService,
        ModalErrorHandler errorHandler,
        BookGateService gate)
    {
        _apiClient = apiClient;
        LivesService = livesService;
        _errorHandler = errorHandler;
        LivesService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LivesService.Lives) or nameof(LivesService.LpPoints)) MainThread.BeginInvokeOnMainThread(OnLivesChanged);
        };
        _gate = gate;
    }

    // >0: played as a step of the check that opens this book (see BookGateService)
    [ObservableProperty] private int _gateBook;

    partial void OnGateBookChanged(int value)
    {
        OnPropertyChanged(nameof(IsLocked));
        if (value > 0) _ = LoadGameAsync();
    }

    partial void OnSubjectIdChanged(int value)
    {
        if (value > 0) _ = LoadGameAsync();
    }

    private async Task LoadGameAsync()
    {
        try
        {
            IsBusy = true;
            IsInteractive = false;
            IsGameWon = false;
            IsGameOver = false;
            HasNoData = false;
            IsDeafMode = false;
            Step = 0;
            _currentStepIndex = -1;
            _allWords.Clear();
            _stepTargets.Clear();
            AllCards.Clear();

            await LivesService.CheckRegenAsync();
            if (IsLocked)
            {
                IsBusy = false;
                return;
            }

            var userWordsTask = _apiClient.GetUserWordsAsync();        // asked at the same time as the learner

            var user = await _apiClient.GetCurrentUserAsync();
            _currentUserId = user?.UserId ?? 0;

            int studyLangId = UserLanguagePrefs.StudyLangId;
            int transLangId = UserLanguagePrefs.TransLangId;

            var allSubjectWords = GateBook > 0
                ? await _apiClient.GetWordsByBookAsync(GateBook - 1, studyLangId, 40)   // the check: words of ALL topics of the previous book
                : await _apiClient.GetAllWordsBySubjectAndLanguageAsync(SubjectId, studyLangId);
            if (allSubjectWords.Count == 0)
            {
                HasNoData = true;
                return;
            }

            var userWords = await userWordsTask;
            var repLookup = userWords.ToDictionary(uw => uw.WordId, uw => uw.RepeatingCount);
            var enWords = allSubjectWords
                .OrderBy(_ => Random.Shared.Next())
                .OrderBy(w => repLookup.TryGetValue(w.WordId, out var cnt) ? cnt : 0)
                .Take(15)
                .ToList();

            await _apiClient.PrefetchWordsAsync(enWords, transLangId);        // all pictures and translations at once
            foreach (var enWord in enWords)
            {
                var ruWord = await _apiClient.GetWordBySvgAndLanguageAsync(enWord.SvgImageId, transLangId);
                var svg = await _apiClient.GetSvgByIdAsync(enWord.SvgImageId);
                _allWords.Add(new WordData(
                    enWord.WordId,
                    enWord.SvgImageId,
                    enWord.WordName,
                    ruWord?.WordName ?? enWord.WordName,
                    SvgHtmlWrapper.Wrap(svg),
                    enWord.WordNumber,
                    enWord.Level ?? string.Empty));
            }

            var pool = _allWords.OrderBy(_ => Random.Shared.Next()).ToList();
            for (int i = 0; i < TotalSteps; i++)
                _stepTargets.Add(pool[i % pool.Count]);

            IsBusy = false;
            await LoadStepAsync(0);
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
            IsBusy = false;
        }
    }

    private async Task LoadStepAsync(int stepIndex)
    {
        _currentStepIndex = stepIndex;
        _currentTarget = _stepTargets[stepIndex];
        TargetWordText = _currentTarget.EnglishWord;

        var distractors = _allWords
            .Where(w => w.SvgImageId != _currentTarget.SvgImageId && !Jumbo.Shared.WordMatch.SameMeaning(w.TranslationWord, _currentTarget.TranslationWord))
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();

        while (distractors.Count < 3)
            distractors.Add(_allWords.First(w => w != _currentTarget));

        var cards = new List<ListenCardViewModel>
        {
            CreateCard(_currentTarget, isTarget: true),
            CreateCard(distractors[0], isTarget: false),
            CreateCard(distractors[1], isTarget: false),
            CreateCard(distractors[2], isTarget: false)
        };
        var shuffled = cards.OrderBy(_ => Random.Shared.Next()).ToList();

        AllCards.Clear();
        foreach (var c in shuffled)
            AllCards.Add(c);

        IsInteractive = true;
        await SpeakTargetAsync();
    }

    private static ListenCardViewModel CreateCard(WordData w, bool isTarget) => new()
    {
        SvgImageId = w.SvgImageId,
        TranslationWord = w.TranslationWord,
        SvgHtml = w.SvgHtml,
        Number = w.Number,
        Level = w.Level,
        IsTarget = isTarget
    };

    private async Task SpeakTargetAsync()
    {
        if (_currentTarget is null) return;

        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith("en"));

            await TextToSpeech.Default.SpeakAsync(_currentTarget.EnglishWord, new SpeechOptions
            {
                Locale = locale,
                Pitch = 1.0f,
                Volume = 1.0f
            }, _ttsCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    [RelayCommand]
    private async Task SelectCard(ListenCardViewModel card)
    {
        if (!IsInteractive || card is null) return;
        IsInteractive = false;

        if (card.IsTarget || (_currentTarget is not null && Jumbo.Shared.WordMatch.SameMeaning(card.TranslationWord, _currentTarget.TranslationWord)))
        {
            Coach.Cheer();
            card.ResultState = ListenCardResultState.Correct;
            await Task.Delay(900);

            Step++;
            if (Step >= TotalSteps)
            {
                IsGameWon = true;
                if (GateBook > 0) _gate.Pass(GateBook, 2);
                else
                {
                    await _apiClient.EnsureWordsExistAsync(_allWords.Select(w => w.WordId).ToList());
                    LivesService.Apply(await _apiClient.MarkGameWonAsync(SubjectId, "ListenAndFind"));      // the server gives the LP (LpReward is only what is shown)
                }
            }
            else
            {
                AllCards.Clear();
                await LoadStepAsync(_currentStepIndex + 1);
            }
        }
        else
        {
            Coach.Oops();
            card.ResultState = ListenCardResultState.Wrong;

            bool canPlay = await HitAsync();
            await Task.Delay(600);
            card.ResultState = ListenCardResultState.None;

            if (!canPlay)
            {
                IsGameOver = true;
            }
            else
            {
                IsInteractive = true;
            }
        }
    }

    [RelayCommand]
    private async Task SpeakWord() => await SpeakTargetAsync();

    [RelayCommand]
    private void ToggleDeafMode() => IsDeafMode = !IsDeafMode;

    [RelayCommand]
    private async Task RestartGame()
    {
        _ttsCts?.Cancel();
        await LivesService.CheckRegenAsync();
        if (IsLocked) return;
        await LoadGameAsync();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        _ttsCts?.Cancel();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand] private void NavigatedTo() { }
    [RelayCommand] private void NavigatedFrom() => _ttsCts?.Cancel();

    [RelayCommand]
    private async Task Appearing()
    {
        if (SubjectId > 0 && !IsBusy && _allWords.Count == 0)
            await LoadGameAsync();
    }

}

public enum ListenCardResultState { None, Correct, Wrong }

public partial class ListenCardViewModel : ObservableObject
{
    public int SvgImageId { get; set; }
    public string TranslationWord { get; set; } = string.Empty;
    public string SvgHtml { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Level { get; set; } = string.Empty;
    public bool IsTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrect), nameof(IsWrong))]
    private ListenCardResultState _resultState = ListenCardResultState.None;

    public bool IsCorrect => ResultState == ListenCardResultState.Correct;
    public bool IsWrong => ResultState == ListenCardResultState.Wrong;
}
