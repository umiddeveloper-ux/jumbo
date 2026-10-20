using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(SubjectId), "subjectId")]
public partial class StepsGamePageModel : ObservableObject
{
    private const int MaxSteps = 15;

    private const int LpReward = 10;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;

    public LivesService LivesService { get; }

    /// <summary>Jumbo the coach: cheers right answers, comforts after wrong ones (bind to a TigerCoach).</summary>
    public GameCoach Coach { get; } = new();

    public string RewardText => LocalizationManager.Format("Common_LpEarned", LpReward);

    private record WordData(int WordId, int SvgImageId, string EnglishWord, string TranslationWord, string SvgHtml);

    private List<WordData> _gameWords = [];
    private int _questionIndex = -1; // 0-based, текущий вопрос в _gameWords
    private int _currentUserId;

    // ── QueryProperty ──────────────────────────────────────────────
    [ObservableProperty] private int _subjectId;

    // ── Состояния ─────────────────────────────────────────────────
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isInteractive;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private bool _hasNoData;
    [ObservableProperty] private bool _showAudienceHelp;

    // ── Текущий вопрос ────────────────────────────────────────────
    [ObservableProperty] private string _currentSvgHtml = string.Empty;
    [ObservableProperty] private string _currentTranslation = string.Empty;

    // ── Подсказки ─────────────────────────────────────────────────
    [ObservableProperty] private bool _canUseFiftyFifty = true;
    [ObservableProperty] private bool _canUseAudienceHelp = true;

    // ── Прогресс (ступеньки) ──────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StepDisplay))]
    private int _currentStep = 0;   // 1-based (0 = ещё не начали)

    [ObservableProperty] private int _totalSteps;

    public string StepDisplay => _currentStep > 0 ? LocalizationManager.Format("Game_Step", _currentStep) : string.Empty;

    // ── Коллекции ─────────────────────────────────────────────────
    public ObservableCollection<StepAnswerViewModel> Answers { get; } = [];
    public ObservableCollection<StepItemViewModel> Steps { get; } = [];
    public ObservableCollection<AudienceResultViewModel> AudienceResults { get; } = [];

    // ── DI ────────────────────────────────────────────────────────
    // No lives and no LP: the 5-hour block (the lock panel with the clock).
    public bool IsLocked => LivesService.IsBlocked;
    private bool _wasLocked;
    private void OnLivesChanged()
    {
        OnPropertyChanged(nameof(IsLocked));
        if (IsLocked) _wasLocked = true;
        else if (_wasLocked) { _wasLocked = false; _ = LoadGameAsync(); }     // the block is over: the game starts
    }

    public StepsGamePageModel(
        LpApiClient apiClient,
        LivesService livesService,
        ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        LivesService = livesService;
        _errorHandler = errorHandler;
        LivesService.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LivesService.Lives) or nameof(LivesService.LpPoints)) MainThread.BeginInvokeOnMainThread(OnLivesChanged);
        };
    }

    partial void OnSubjectIdChanged(int value)
    {
        if (value > 0) _ = LoadGameAsync();
    }

    // ── Загрузка ──────────────────────────────────────────────────
    private async Task LoadGameAsync()
    {
        try
        {
            IsBusy = true;
            ResetAll();

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

            var allSubjectWords = await _apiClient.GetAllWordsBySubjectAndLanguageAsync(SubjectId, studyLangId);
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
                .Take(MaxSteps)
                .ToList();

            // Собираем слова
            await _apiClient.PrefetchWordsAsync(enWords, transLangId);        // all pictures and translations at once
            _gameWords.Clear();
            foreach (var enWord in enWords)
            {
                var ruWord = await _apiClient.GetWordBySvgAndLanguageAsync(enWord.SvgImageId, transLangId);
                var svg = await _apiClient.GetSvgByIdAsync(enWord.SvgImageId);
                _gameWords.Add(new WordData(
                    enWord.WordId,
                    enWord.SvgImageId,
                    enWord.WordName,
                    ruWord?.WordName ?? enWord.WordName,
                    SvgHtmlWrapper.Wrap(svg)));
            }
            _gameWords = [.. _gameWords.OrderBy(_ => Random.Shared.Next())];

            TotalSteps = _gameWords.Count;

            // Строим список ступенек (сверху — высшая, снизу — первая)
            Steps.Clear();
            for (int i = TotalSteps; i >= 1; i--)
                Steps.Add(new StepItemViewModel
                {
                    StepNumber = i,
                    IsMilestone = i % 5 == 0 || i == TotalSteps
                });

            IsBusy = false;

            _questionIndex = 0;
            CurrentStep = 1;
            UpdateStepHighlighting();
            await LoadQuestionAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
            IsBusy = false;
        }
    }

    // ── Загрузка вопроса ──────────────────────────────────────────
    private async Task LoadQuestionAsync()
    {
        var target = _gameWords[_questionIndex];
        CurrentSvgHtml = target.SvgHtml;
        CurrentTranslation = target.TranslationWord;

        // 3 дистрактора
        var distractors = _gameWords
            .Where(w => w.SvgImageId != target.SvgImageId && !Jumbo.Shared.WordMatch.SameMeaning(w.TranslationWord, target.TranslationWord))
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();
        while (distractors.Count < 3)
            distractors.Add(_gameWords.First(w => w != target));

        // Формируем 4 варианта и перемешиваем
        var raw = new List<(string Word, bool IsTarget)>
        {
            (target.EnglishWord, true),
            (distractors[0].EnglishWord, false),
            (distractors[1].EnglishWord, false),
            (distractors[2].EnglishWord, false)
        };
        var shuffled = raw.OrderBy(_ => Random.Shared.Next()).ToList();
        string[] letters = ["A", "B", "C", "D"];

        Answers.Clear();
        for (int i = 0; i < shuffled.Count; i++)
        {
            Answers.Add(new StepAnswerViewModel
            {
                Letter = letters[i],
                Word = shuffled[i].Word,
                IsTarget = shuffled[i].IsTarget
            });
        }

        // Короткая пауза для плавного обновления UI
        await Task.Delay(50);
        IsInteractive = true;
    }

    // ── Выбор ответа ──────────────────────────────────────────────
    [RelayCommand]
    private async Task SelectAnswer(StepAnswerViewModel answer)
    {
        if (!IsInteractive || answer is null) return;
        IsInteractive = false;

        if (answer.IsTarget)
        {
            Coach.Cheer();
            answer.State = StepAnswerState.Correct;
            await Task.Delay(900);

            _questionIndex++;
            if (_questionIndex >= _gameWords.Count)
            {
                CurrentStep = TotalSteps;
                UpdateStepHighlighting();
                IsGameWon = true;
                await _apiClient.EnsureWordsExistAsync(_gameWords.Select(w => w.WordId).ToList());
                await _apiClient.MarkGameWonAsync(SubjectId, "Steps");
                await LivesService.AddLpAsync(LpReward);
                return;
            }

            CurrentStep++;
            UpdateStepHighlighting();
            await LoadQuestionAsync();
        }
        else
        {
            Coach.Oops();
            answer.State = StepAnswerState.Wrong;
            await LivesService.TakeDamageAsync();

            var correct = Answers.FirstOrDefault(a => a.IsTarget);
            if (correct != null)
            {
                await Task.Delay(400);
                correct.State = StepAnswerState.Correct;
            }
            await Task.Delay(1500);
            IsGameOver = true;
        }
    }

    // ── Подсказка 50/50 ───────────────────────────────────────────
    [RelayCommand]
    private void UseFiftyFifty()
    {
        if (!CanUseFiftyFifty) return;
        CanUseFiftyFifty = false;

        var wrong = Answers
            .Where(a => !a.IsTarget && a.IsVisible)
            .OrderBy(_ => Random.Shared.Next())
            .Take(2)
            .ToList();

        foreach (var a in wrong)
            a.IsVisible = false;
    }

    // ── Подсказка: помощь зала ────────────────────────────────────
    [RelayCommand]
    private void UseAudienceHelp()
    {
        if (!CanUseAudienceHelp) return;
        CanUseAudienceHelp = false;

        AudienceResults.Clear();

        int correctPct = Random.Shared.Next(62, 82);
        var visibleWrong = Answers.Where(a => !a.IsTarget && a.IsVisible).ToList();
        var wrongPcts = SplitPercent(100 - correctPct, visibleWrong.Count);

        int wi = 0;
        foreach (var ans in Answers)
        {
            int pct = !ans.IsVisible ? 0 : ans.IsTarget ? correctPct : wrongPcts[wi++];
            AudienceResults.Add(new AudienceResultViewModel
            {
                Letter = ans.Letter,
                Word = ans.Word,
                Percentage = pct,
                IsTarget = ans.IsTarget
            });
        }

        ShowAudienceHelp = true;
    }

    [RelayCommand]
    private void CloseAudienceHelp() => ShowAudienceHelp = false;

    // ── Вспомогательные ───────────────────────────────────────────
    private void UpdateStepHighlighting()
    {
        foreach (var s in Steps)
        {
            s.IsCurrent = s.StepNumber == CurrentStep;
            s.IsPassed = s.StepNumber < CurrentStep;
        }
    }

    private static List<int> SplitPercent(int total, int count)
    {
        if (count <= 0) return [];
        var parts = new List<int>();
        int rem = total;
        for (int i = 0; i < count - 1; i++)
        {
            int p = Random.Shared.Next(1, Math.Max(2, rem - (count - i - 1)));
            parts.Add(p);
            rem -= p;
        }
        parts.Add(Math.Max(0, rem));
        return [.. parts.OrderBy(_ => Random.Shared.Next())];
    }

    private void ResetAll()
    {
        _gameWords.Clear();
        _questionIndex = -1;
        Answers.Clear();
        Steps.Clear();
        AudienceResults.Clear();
        IsInteractive = false;
        IsGameWon = false;
        IsGameOver = false;
        HasNoData = false;
        ShowAudienceHelp = false;
        CanUseFiftyFifty = true;
        CanUseAudienceHelp = true;
        CurrentStep = 0;
        TotalSteps = 0;
        CurrentSvgHtml = string.Empty;
        CurrentTranslation = string.Empty;
    }

    // ── Shell-команды ─────────────────────────────────────────────
    [RelayCommand] private void NavigatedTo() { }
    [RelayCommand] private void NavigatedFrom() { }

    [RelayCommand]
    private async Task Appearing()
    {
        if (SubjectId > 0 && !IsBusy && _gameWords.Count == 0)
            await LoadGameAsync();
    }

    [RelayCommand]
    private async Task RestartGame()
    {
        await LivesService.CheckRegenAsync();
        if (IsLocked) return;
        ResetAll();
        await LoadGameAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.GoToAsync("..");
}

// ═══════════════════════════════════════════════════════════════════
// ViewModels
// ═══════════════════════════════════════════════════════════════════

public enum StepAnswerState { Normal, Correct, Wrong }

public partial class StepAnswerViewModel : ObservableObject
{
    public string Letter { get; set; } = string.Empty;
    public string Word { get; set; } = string.Empty;
    public bool IsTarget { get; set; }

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrect), nameof(IsWrong))]
    private StepAnswerState _state = StepAnswerState.Normal;

    public bool IsCorrect => State == StepAnswerState.Correct;
    public bool IsWrong => State == StepAnswerState.Wrong;
}

public partial class StepItemViewModel : ObservableObject
{
    private int _stepNumber;
    public int StepNumber
    {
        get => _stepNumber;
        set { _stepNumber = value; StepNumberText = value.ToString(); }
    }
    public string StepNumberText { get; private set; } = string.Empty;
    public bool IsMilestone { get; set; }

    [ObservableProperty] private bool _isCurrent;
    [ObservableProperty] private bool _isPassed;
}

public class AudienceResultViewModel
{
    public string Letter { get; set; } = string.Empty;
    public string Word { get; set; } = string.Empty;
    public int Percentage { get; set; }
    public bool IsTarget { get; set; }
    public double ProgressValue => Percentage / 100.0;
    public string PercentageText => $"{Percentage}%";
}
