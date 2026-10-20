using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;

namespace Jumbo.PageModels;

[QueryProperty(nameof(Letter), "letter")]
public partial class LetterRecallPageModel : ObservableObject
{
    private const int MaxSteps = 10;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;

    private record WordData(int WordId, int SvgImageId, string EnglishWord, string RussianWord, string SvgHtml);

    private List<WordData> _words = [];
    private int _stepIndex = -1;
    private int _correctCount = 0;
    private int _currentUserId;

    [ObservableProperty] private string _letter = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isWordRevealed;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _hasNoData;

    [ObservableProperty] private string _currentSvgHtml = string.Empty;
    [ObservableProperty] private string _currentRussianWord = string.Empty;
    [ObservableProperty] private string _currentEnglishWord = string.Empty;
    [ObservableProperty] private string _summaryText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressValue), nameof(ProgressText))]
    private int _step = 0;

    [ObservableProperty] private int _totalSteps;

    public double ProgressValue => TotalSteps > 0 ? (double)_step / TotalSteps : 0;
    public string ProgressText => $"{_step} / {TotalSteps}";

    public LetterRecallPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    partial void OnLetterChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) _ = LoadGameAsync();
    }

    // ── Загрузка ──────────────────────────────────────────────────
    private async Task LoadGameAsync()
    {
        try
        {
            IsBusy = true;
            _words.Clear();
            _stepIndex = -1;
            _correctCount = 0;
            Step = 0;
            IsGameWon = false;
            HasNoData = false;
            IsWordRevealed = false;

            int studyLangId = UserLanguagePrefs.StudyLangId;
            int transLangId = UserLanguagePrefs.TransLangId;

            var userWordsTask = _apiClient.GetUserWordsAsync();        // asked at the same time as the learner

            var user = await _apiClient.GetCurrentUserAsync();
            _currentUserId = user?.UserId ?? 0;

            var enWords = await _apiClient.GetWordsByLetterAsync(Letter, studyLangId, 500);
            if (enWords.Count == 0)
            {
                HasNoData = true;
                return;
            }

            var userWords = await userWordsTask;
            var repLookup = userWords.ToDictionary(uw => uw.WordId, uw => uw.RepeatingCount);

            await _apiClient.PrefetchWordsAsync(enWords, transLangId);        // all pictures and translations at once
            foreach (var enWord in enWords)
            {
                var ruWord = await _apiClient.GetWordBySvgAndLanguageAsync(enWord.SvgImageId, transLangId);
                var svg = await _apiClient.GetSvgByIdAsync(enWord.SvgImageId);
                _words.Add(new WordData(
                    enWord.WordId,
                    enWord.SvgImageId,
                    enWord.WordName,
                    ruWord?.WordName ?? enWord.WordName,
                    SvgHtmlWrapper.Wrap(svg)));
            }

            _words = [.. _words
                .OrderBy(_ => Random.Shared.Next())
                .OrderBy(w => repLookup.TryGetValue(w.WordId, out var cnt) ? cnt : 0)];
            TotalSteps = Math.Min(MaxSteps, _words.Count);

            IsBusy = false;
            _stepIndex = 0;
            Step = 1;
            LoadCard();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
            IsBusy = false;
        }
    }

    private void LoadCard()
    {
        var word = _words[_stepIndex];
        CurrentSvgHtml = word.SvgHtml;
        CurrentRussianWord = word.RussianWord;
        CurrentEnglishWord = word.EnglishWord;
        IsWordRevealed = false;
    }

    // ── Показать слово ────────────────────────────────────────────
    [RelayCommand]
    private void RevealWord() => IsWordRevealed = true;

    // ── Ответ ─────────────────────────────────────────────────────
    [RelayCommand]
    private async Task AnswerCorrect()
    {
        _correctCount++;
        await _apiClient.IncrementWordRepetitionAsync(_words[_stepIndex].WordId);
        NextStep();
    }

    [RelayCommand]
    private void AnswerWrong() => NextStep();

    private void NextStep()
    {
        _stepIndex++;
        Step++;

        if (Step > TotalSteps || _stepIndex >= _words.Count)
        {
            SummaryText = LocalizationManager.Format("Letter_RecalledCount", _correctCount, MaxSteps);
            IsGameWon = true;
        }
        else
        {
            LoadCard();
        }
    }

    // ── Shell-команды ─────────────────────────────────────────────
    [RelayCommand] private void NavigatedTo() { }
    [RelayCommand] private void NavigatedFrom() { }

    [RelayCommand]
    private async Task Appearing()
    {
        if (!string.IsNullOrEmpty(Letter) && !IsBusy && _words.Count == 0)
            await LoadGameAsync();
    }

    [RelayCommand]
    private async Task RestartGame()
    {
        _words.Clear();
        await LoadGameAsync();
    }

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.GoToAsync("..");

}
