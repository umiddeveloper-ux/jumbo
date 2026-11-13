using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(SubjectId), "subjectId")]
[QueryProperty(nameof(GateBook), "gate")]
public partial class FindMeGamePageModel : ObservableObject
{
    private const int LpReward = 5;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private readonly BookGateService _gate;

    public LivesService LivesService { get; }

    /// <summary>Jumbo the coach: cheers right answers, comforts after wrong ones (bind to a TigerCoach).</summary>
    public GameCoach Coach { get; } = new();

    public string RewardText => GateBook > 0 ? string.Empty : LocalizationManager.Format("Common_LpEarned", LpReward);

    // Only SlotCount cards are on the screen: a found word is replaced by the next one until all the words of the topic have come,
    // and the last cards turn into ticks one by one (same game as on a phone's website).
    private const int SlotCount = 6;

    private List<GameCardViewModel> _wordSequence = [];     // all the words of this round
    private Queue<GameCardViewModel> _unseen = new();       // words that have not come on the screen yet
    private GameCardViewModel? _target;
    private int _foundCount;
    private int _currentWordIndex = -1;
    private int _currentUserId;

    [ObservableProperty] private int _subjectId;
    // The width of a card (the page sets it from its own width: three cards in a row).
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LabelWidth))] private double _cardWidth = 110;
    public double LabelWidth => Math.Max(40, CardWidth - 12);
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(BannerText))] private string _currentWord = string.Empty;

    // The banner next to Jumbo: the word to find, or (for a moment) what Jumbo says about the answer.
    public string BannerText => Coach.HasText ? Coach.Text : CurrentWord;

    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _hasNoData;

    public ObservableCollection<GameCardViewModel> Cards { get; } = [];

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

    public FindMeGamePageModel(
        LpApiClient apiClient,
        LivesService livesService,
        ModalErrorHandler errorHandler,
        BookGateService gate)
    {
        _apiClient = apiClient;
        LivesService = livesService;
        Coach.PropertyChanged += (_, _) => OnPropertyChanged(nameof(BannerText));      // the banner shows the coach's phrase for a moment
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
        if (value > 0)
            _ = LoadGameAsync();
    }

    private async Task LoadGameAsync()
    {
        try
        {
            IsBusy = true;
            IsGameOver = false;
            IsGameWon = false;
            IsPlaying = false;
            HasNoData = false;
            Coach.Reset();
            Cards.Clear();
            _wordSequence.Clear();
            _unseen.Clear();
            _target = null;
            _foundCount = 0;
            _currentWordIndex = -1;
            CurrentWord = string.Empty;

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
            var cards = new List<GameCardViewModel>();
            foreach (var enWord in enWords)
            {
                var ruWord = await _apiClient.GetWordBySvgAndLanguageAsync(enWord.SvgImageId, transLangId);
                var rawSvg = await _apiClient.GetSvgByIdAsync(enWord.SvgImageId);

                cards.Add(new GameCardViewModel
                {
                    WordId = enWord.WordId,
                    SvgImageId = enWord.SvgImageId,
                    EnglishWord = enWord.WordName,
                    TranslationWord = ruWord?.WordName ?? enWord.WordName,
                    SvgHtml = SvgHtmlWrapper.Wrap(rawSvg),
                    Number = enWord.WordNumber,
                    Level = enWord.Level ?? string.Empty
                });
            }

            _wordSequence = cards.OrderBy(_ => Random.Shared.Next()).ToList();
            foreach (var card in _wordSequence.Take(SlotCount))
                Cards.Add(card);
            _unseen = new Queue<GameCardViewModel>(_wordSequence.Skip(SlotCount));
            PickTarget();
            IsPlaying = true;
#if DEBUG
            // Screenshots: JUMBO_OVERLAY=win|lose shows that end-of-game panel straight away.
            switch (Environment.GetEnvironmentVariable("JUMBO_OVERLAY"))
            {
                case "win": IsGameWon = true; IsPlaying = false; Coach.Cheer(); break;
                case "lose": IsGameOver = true; IsPlaying = false; break;
                case "coach-ok": Coach.Cheer(); break;
                case "coach-no": Coach.Oops(); break;
            }
#endif
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // The next word to find: any card still on the screen that is not a tick yet.
    private void PickTarget()
    {
        var open = Cards.Where(c => !c.IsMatched).ToList();
        if (open.Count == 0) return;
        _target = open[Random.Shared.Next(open.Count)];
        CurrentWord = _target.EnglishWord;
    }

    [RelayCommand]
    private async Task SelectCard(GameCardViewModel card)
    {
        if (card is null || card.IsMatched || !IsPlaying || _target is null) return;

        // right: the word itself, or any card with the same translation ("bye" / "goodbye" are both "до свидания")
        if (card.SvgImageId == _target.SvgImageId || Jumbo.Shared.WordMatch.SameMeaning(card.TranslationWord, _target.TranslationWord))
        {
            Coach.Cheer();
            _foundCount++;

            // the found card gives its place to the next word; when there is none left, it turns into a tick
            var at = Cards.IndexOf(card);
            if (_unseen.Count > 0 && at >= 0) Cards[at] = _unseen.Dequeue();
            else card.IsMatched = true;

            if (_foundCount >= _wordSequence.Count)
            {
                IsGameWon = true;
                IsPlaying = false;
                CurrentWord = string.Empty;
                if (GateBook > 0) _gate.Pass(GateBook, 1);
                else
                {
                    await _apiClient.EnsureWordsExistAsync(_wordSequence.Select(c => c.WordId).ToList());
                    LivesService.Apply(await _apiClient.MarkGameWonAsync(SubjectId, "FindMe"));      // the server gives the LP (LpReward is only what is shown)
                    _topicDone = await _apiClient.GetBookIfTopicDoneAsync(SubjectId) is not null;      // all three games won: "Continue" goes to the snake
                }
            }
            else
            {
                PickTarget();
            }
        }
        else
        {
            Coach.Oops();
            card.ShowWrong = true;
            Task.Delay(500).ContinueWith(_ =>
            {
                MainThread.BeginInvokeOnMainThread(() => card.ShowWrong = false);
            });

            if (!await HitAsync())
            {
                IsGameOver = true;
                IsPlaying = false;
            }
        }
    }

    [RelayCommand]
    private async Task RestartGame()
    {
        await LivesService.CheckRegenAsync();
        if (IsLocked) return;
        await LoadGameAsync();
    }

    private bool _topicDone;      // this win finished the topic

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.GoToAsync(IsGameWon && _topicDone ? "../.." : "..");      // "../..": past the topic page to the snake

    [RelayCommand]
    private void NavigatedTo() { }

    [RelayCommand]
    private void NavigatedFrom() { }

    [RelayCommand]
    private async Task Appearing()
    {
        if (SubjectId > 0 && !IsPlaying && !IsBusy)
            await LoadGameAsync();
    }

}

public partial class GameCardViewModel : ObservableObject
{
    public int WordId { get; set; }
    public int SvgImageId { get; set; }
    public string EnglishWord { get; set; } = string.Empty;
    public string TranslationWord { get; set; } = string.Empty;
    public string SvgHtml { get; set; } = string.Empty;
    public int Number { get; set; }          // the number of the word (on the printed card)
    public string Level { get; set; } = string.Empty;       // A1 ... (on the printed card)

    // A long translation gets a smaller font so that it fits the card in one line (a label of a card did not wrap on Android).
    public double TranslationFontSize => TranslationWord.Length switch { <= 8 => 15, <= 11 => 13, <= 14 => 11.5, _ => 10.5 };

    [ObservableProperty] private bool _isMatched;
    [ObservableProperty] private bool _showWrong;
}
