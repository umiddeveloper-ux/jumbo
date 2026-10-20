using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(Letter), "letter")]
public partial class LetterTestPageModel : ObservableObject
{
    private const int MaxSteps = 10;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;

    private record WordData(int WordId, int SvgImageId, string EnglishWord, string RussianWord, string SvgHtml);

    private List<WordData> _words = [];
    private int _stepIndex = -1;
    private int _currentUserId;

    [ObservableProperty] private string _letter = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isInteractive;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _hasNoData;

    [ObservableProperty] private string _currentSvgHtml = string.Empty;
    [ObservableProperty] private string _currentEnglishWord = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressValue), nameof(ProgressText))]
    private int _step = 0;

    [ObservableProperty] private int _totalSteps;

    public double ProgressValue => TotalSteps > 0 ? (double)_step / TotalSteps : 0;
    public string ProgressText => $"{_step} / {TotalSteps}";

    public ObservableCollection<LetterOptionViewModel> Options { get; } = [];

    public LetterTestPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
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
            Step = 0;
            IsGameWon = false;
            HasNoData = false;
            Options.Clear();

            int studyLangId = UserLanguagePrefs.StudyLangId;
            int transLangId = UserLanguagePrefs.TransLangId;

            var userWordsTask = _apiClient.GetUserWordsAsync();        // asked at the same time as the learner

            var user = await _apiClient.GetCurrentUserAsync();
            _currentUserId = user?.UserId ?? 0;

            // Получаем все слова буквы (без ограничения) для правильной сортировки
            var enWords = await _apiClient.GetWordsByLetterAsync(Letter, studyLangId, 500);
            if (enWords.Count < 4)
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

            // Сначала перемешиваем (для разнообразия внутри группы), затем стабильная сортировка по числу повторений
            _words = [.. _words
                .OrderBy(_ => Random.Shared.Next())
                .OrderBy(w => repLookup.TryGetValue(w.WordId, out var cnt) ? cnt : 0)];
            TotalSteps = Math.Min(MaxSteps, _words.Count);

            IsBusy = false;
            _stepIndex = 0;
            Step = 1;
            await LoadStepAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
            IsBusy = false;
        }
    }

    private async Task LoadStepAsync()
    {
        var target = _words[_stepIndex];
        CurrentSvgHtml = target.SvgHtml;
        CurrentEnglishWord = target.EnglishWord;

        // 3 дистрактора (другие русские слова)
        var distractors = _words
            .Where(w => w.SvgImageId != target.SvgImageId && !Jumbo.Shared.WordMatch.SameMeaning(w.RussianWord, target.RussianWord))
            .OrderBy(_ => Random.Shared.Next())
            .Take(3)
            .ToList();

        while (distractors.Count < 3)
            distractors.Add(_words.First(w => w != target));

        var raw = new List<(string Translation, bool IsTarget)>
        {
            (target.RussianWord, true),
            (distractors[0].RussianWord, false),
            (distractors[1].RussianWord, false),
            (distractors[2].RussianWord, false),
        };

        Options.Clear();
        foreach (var opt in raw.OrderBy(_ => Random.Shared.Next()))
        {
            Options.Add(new LetterOptionViewModel
            {
                Translation = opt.Translation,
                IsTarget = opt.IsTarget
            });
        }

        await Task.Delay(50);
        IsInteractive = true;
    }

    // ── Выбор ответа ──────────────────────────────────────────────
    [RelayCommand]
    private async Task SelectOption(LetterOptionViewModel option)
    {
        if (!IsInteractive || option is null) return;
        IsInteractive = false;

        if (option.IsTarget)
        {
            option.State = LetterOptionState.Correct;
            await _apiClient.IncrementWordRepetitionAsync(_words[_stepIndex].WordId);
            await Task.Delay(700);

            _stepIndex++;
            Step++;

            if (Step > TotalSteps || _stepIndex >= _words.Count || Step > MaxSteps)
            {
                IsGameWon = true;
            }
            else
            {
                option.State = LetterOptionState.Normal;
                await LoadStepAsync();
            }
        }
        else
        {
            option.State = LetterOptionState.Wrong;
            var correct = Options.FirstOrDefault(o => o.IsTarget);
            if (correct != null)
            {
                await Task.Delay(400);
                correct.State = LetterOptionState.Correct;
            }
            await Task.Delay(1200);

            // Сбрасываем и повторяем тот же шаг
            foreach (var o in Options) o.State = LetterOptionState.Normal;
            IsInteractive = true;
        }
    }

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

// ── ViewModels ─────────────────────────────────────────────────────
public enum LetterOptionState { Normal, Correct, Wrong }

public partial class LetterOptionViewModel : ObservableObject
{
    public string Translation { get; set; } = string.Empty;
    public bool IsTarget { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrect), nameof(IsWrong))]
    private LetterOptionState _state = LetterOptionState.Normal;

    public bool IsCorrect => State == LetterOptionState.Correct;
    public bool IsWrong => State == LetterOptionState.Wrong;
}
