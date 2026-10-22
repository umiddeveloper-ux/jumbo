using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(SubjectId), "subjectId")]
[QueryProperty(nameof(GateBook), "gate")]
public partial class SequenceGamePageModel : ObservableObject
{
    private const int LpReward = 7;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private readonly BookGateService _gate;

    public LivesService LivesService { get; }

    /// <summary>Jumbo the coach: cheers right answers, comforts after wrong ones (bind to a TigerCoach).</summary>
    public GameCoach Coach { get; } = new();

    public string RewardText => GateBook > 0 ? string.Empty : LocalizationManager.Format("Common_LpEarned", LpReward);

    private List<List<SequenceCardViewModel>> _rounds = [];
    private int _currentRoundIndex = -1;
    private int _selectedCount = 0;
    private int _currentUserId;
    private CancellationTokenSource? _cts;

    [ObservableProperty] private int _subjectId;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isTopAreaVisible;
    [ObservableProperty] private bool _isSelecting;
    [ObservableProperty] private bool _isRoundSuccess;
    [ObservableProperty] private bool _isRoundFail;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private bool _hasNoData;

    [ObservableProperty] private string _topText = string.Empty;
    [ObservableProperty] private bool _isCountdownText;

    [ObservableProperty] private int _currentRound;
    [ObservableProperty] private int _totalRounds;

    public ObservableCollection<SequenceCardViewModel> Cards { get; } = [];

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

    public SequenceGamePageModel(
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
            ResetAllState();

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
            var allCards = new List<SequenceCardViewModel>();
            foreach (var enWord in enWords)
            {
                var ruWord = await _apiClient.GetWordBySvgAndLanguageAsync(enWord.SvgImageId, transLangId);
                var svg = await _apiClient.GetSvgByIdAsync(enWord.SvgImageId);

                allCards.Add(new SequenceCardViewModel
                {
                    WordId = enWord.WordId,
                    SvgImageId = enWord.SvgImageId,
                    EnglishWord = enWord.WordName,
                    TranslationWord = ruWord?.WordName ?? enWord.WordName,
                    SvgHtml = SvgHtmlWrapper.Wrap(svg)
                });
            }

            var shuffled = allCards.OrderBy(_ => Random.Shared.Next()).ToList();
            _rounds.Clear();

            for (int i = 0; i < shuffled.Count; i += 4)
            {
                var chunk = shuffled.Skip(i).Take(4).ToList();
                if (chunk.Count < 2) break;

                for (int j = 0; j < chunk.Count; j++)
                    chunk[j].CorrectOrder = j + 1;

                _rounds.Add(chunk);
                if (_rounds.Count == 4) break;
            }

            TotalRounds = _rounds.Count;
            _currentRoundIndex = 0;
            CurrentRound = 1;

            IsBusy = false;
            await RunRoundSequenceAsync();
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
            IsBusy = false;
        }
    }

    private async Task RunRoundSequenceAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            var round = _rounds[_currentRoundIndex];

            IsSelecting = false;
            IsTopAreaVisible = true;

            for (int i = 3; i >= 1; i--)
            {
                IsCountdownText = true;
                TopText = i.ToString();
                await Task.Delay(800, token);
            }

            IsCountdownText = false;
            foreach (var card in round)
            {
                TopText = card.EnglishWord;
                await Task.Delay(700, token);
                TopText = string.Empty;
                await Task.Delay(150, token);
            }

            Cards.Clear();
            _selectedCount = 0;

            var shuffled = round.OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var c in shuffled)
            {
                c.Reset();
                Cards.Add(c);
            }

            TopText = LocalizationManager.Get("SequenceGame_ChooseOrder");
            IsCountdownText = false;
            IsSelecting = true;
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private async Task SelectCard(SequenceCardViewModel card)
    {
        if (!IsSelecting) return;

        if (card.IsSelected)
        {
            // Already picked — tapping it again undoes that pick so a misclick can be corrected.
            var removedOrder = card.SelectedOrder;
            card.SelectedOrder = 0;
            foreach (var c in Cards)
            {
                if (c.SelectedOrder > removedOrder)
                    c.SelectedOrder--;
            }
            _selectedCount--;
            return;
        }

        _selectedCount++;
        card.SelectedOrder = _selectedCount;

        if (_selectedCount == Cards.Count)
            await CheckSequenceAsync();
    }

    private async Task CheckSequenceAsync()
    {
        IsSelecting = false;
        _cts?.Cancel();

        bool correct = Cards.All(c => c.SelectedOrder == c.CorrectOrder);

        if (correct)
        {
            foreach (var c in Cards) c.State = CardState.Correct;

            Coach.Cheer();
            IsRoundSuccess = true;
            await Task.Delay(1400);
            IsRoundSuccess = false;

            _currentRoundIndex++;
            if (_currentRoundIndex >= _rounds.Count)
            {
                IsTopAreaVisible = false;
                IsGameWon = true;
                if (GateBook > 0) _gate.Pass(GateBook, 3);
                else
                {
                    await _apiClient.EnsureWordsExistAsync(_rounds.SelectMany(r => r).Select(c => c.WordId).ToList());
                    LivesService.Apply(await _apiClient.MarkGameWonAsync(SubjectId, "Sequence"));      // the server gives the LP (LpReward is only what is shown)
                }
            }
            else
            {
                CurrentRound = _currentRoundIndex + 1;
                await RunRoundSequenceAsync();
            }
        }
        else
        {
            foreach (var c in Cards)
                c.State = c.SelectedOrder == c.CorrectOrder ? CardState.Correct : CardState.Wrong;

            Coach.Oops();
            IsRoundFail = true;
            var alive = await HitAsync();
            await Task.Delay(2000);
            IsRoundFail = false;

            if (!alive)
            {
                IsTopAreaVisible = false;
                IsGameOver = true;
            }
            else
            {
                await RunRoundSequenceAsync();
            }
        }
    }

    [RelayCommand]
    private async Task RestartGame()
    {
        _cts?.Cancel();
        await LivesService.CheckRegenAsync();
        if (IsLocked) return;
        await LoadGameAsync();
    }

    [RelayCommand]
    private async Task GoBack()
    {
        _cts?.Cancel();
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private void NavigatedTo() { }

    [RelayCommand]
    private void NavigatedFrom() => _cts?.Cancel();

    [RelayCommand]
    private async Task Appearing()
    {
        if (SubjectId > 0 && !IsBusy && _rounds.Count == 0)
            await LoadGameAsync();
    }

    private void ResetAllState()
    {
        _cts?.Cancel();
        _rounds.Clear();
        _currentRoundIndex = -1;
        _selectedCount = 0;
        Cards.Clear();
        IsSelecting = false;
        IsTopAreaVisible = false;
        IsRoundSuccess = false;
        IsRoundFail = false;
        IsGameWon = false;
        IsGameOver = false;
        HasNoData = false;
        TopText = string.Empty;
        CurrentRound = 0;
        TotalRounds = 0;
    }

}

public enum CardState { Idle, Correct, Wrong }

public partial class SequenceCardViewModel : ObservableObject
{
    public int WordId { get; set; }
    public int SvgImageId { get; set; }
    public string EnglishWord { get; set; } = string.Empty;
    public string TranslationWord { get; set; } = string.Empty;
    public string SvgHtml { get; set; } = string.Empty;
    public int CorrectOrder { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSelected))]
    [NotifyPropertyChangedFor(nameof(BadgeText))]
    private int _selectedOrder;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCorrectCard))]
    [NotifyPropertyChangedFor(nameof(IsWrongCard))]
    private CardState _state = CardState.Idle;

    public bool IsSelected => SelectedOrder > 0;
    public string BadgeText => SelectedOrder > 0 ? SelectedOrder.ToString() : string.Empty;
    public bool IsCorrectCard => State == CardState.Correct;
    public bool IsWrongCard => State == CardState.Wrong;

    public void Reset()
    {
        SelectedOrder = 0;
        State = CardState.Idle;
    }
}
