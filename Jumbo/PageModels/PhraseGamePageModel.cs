using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>A word of the phrase: it sits in the bank below, or in a cell above.</summary>
public partial class PhraseTile : ObservableObject
{
    public int Id { get; init; }
    public string Text { get; init; } = string.Empty;

    [ObservableProperty] private bool _isPlaced;
}

/// <summary>A cell of the phrase: empty, filled, or (after a check) right / wrong.</summary>
public partial class PhraseSlot : ObservableObject
{
    public int Index { get; init; }
    public PhraseTile? Tile { get; init; }
    public string Text => Tile?.Text ?? string.Empty;
    public string State { get; init; } = "empty";           // empty | filled | right | wrong
    public bool HasTile => Tile is not null;
}

/// <summary>
/// "Build the phrase" (same game as on the website, inside a topic): a picture and its word, the example in the learner's own
/// language, empty cells and the words of the English phrase mixed up below. A tap on a word puts it in the first free cell; a tap on
/// a word in a cell sends it back. The Next button checks the phrase: right -> the next one; wrong -> a life is lost and the words
/// stay, so they can be moved around.
/// </summary>
[QueryProperty(nameof(SubjectId), "subjectId")]
public partial class PhraseGamePageModel : ObservableObject
{
    private const int MaxRounds = 15;
    private const int LpReward = 8;

    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private CancellationTokenSource? _ttsCts;

    public LivesService LivesService { get; }
    public GameCoach Coach { get; } = new();
    public string RewardText => LocalizationManager.Format("Common_LpEarned", LpReward);

    private record Round(int WordId, string SvgHtml, string Word, string WordTranslation, string Phrase, string Prompt, int Number = 0, string Level = "");

    private List<Round> _rounds = [];
    private List<string> _target = [];
    private List<PhraseTile> _placed = [];            // the cells hold these, from the left, without gaps
    private int _stepIndex;
    private string _verdict = string.Empty;           // "" | right | wrong

    [ObservableProperty] private int _subjectId;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private bool _isInteractive;
    [ObservableProperty] private bool _isGameWon;
    [ObservableProperty] private bool _isGameOver;
    [ObservableProperty] private bool _hasNoData;
    [ObservableProperty] private bool _canCheck;

    [ObservableProperty] private string _svgHtml = string.Empty;
    [ObservableProperty] private string _word = string.Empty;
    [ObservableProperty] private string _wordTranslation = string.Empty;
    [ObservableProperty] private int _wordNumber;
    [ObservableProperty] private string _wordLevel = string.Empty;
    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private string _stepText = string.Empty;

    public ObservableCollection<PhraseSlot> Slots { get; } = [];
    public ObservableCollection<PhraseTile> Bank { get; } = [];

    // No lives and no LP: the 5-hour block (the lock panel with the clock).
    public bool IsLocked => LivesService.IsBlocked;
    private bool _wasLocked;
    private void OnLivesChanged()
    {
        OnPropertyChanged(nameof(IsLocked));
        if (IsLocked) _wasLocked = true;
        else if (_wasLocked) { _wasLocked = false; _ = LoadGameAsync(); }     // the block is over: the game starts
    }

    public PhraseGamePageModel(LpApiClient apiClient, LivesService livesService, ModalErrorHandler errorHandler)
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

    private async Task LoadGameAsync()
    {
        try
        {
            IsBusy = true;
            IsGameWon = IsGameOver = HasNoData = IsPlaying = false;
            Coach.Reset();
            _rounds = [];
            _stepIndex = 0;

            await LivesService.CheckRegenAsync();
            if (IsLocked) return;

            int studyLangId = UserLanguagePrefs.StudyLangId;
            int transLangId = UserLanguagePrefs.TransLangId;

            // Only words that have an example sentence (in the language being studied) can make a round.
            var words = await _apiClient.GetAllWordsBySubjectAndLanguageAsync(SubjectId, studyLangId);
            var withExample = words
                .Where(w => !string.IsNullOrWhiteSpace(w.Example) && w.Example!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
                .OrderBy(_ => Random.Shared.Next()).Take(MaxRounds).ToList();
            if (withExample.Count == 0) { HasNoData = true; return; }

            await _apiClient.PrefetchWordsAsync(withExample, transLangId);        // all pictures and translations at once
            foreach (var w in withExample)
            {
                var trans = await _apiClient.GetWordBySvgAndLanguageAsync(w.SvgImageId, transLangId);
                var svg = await _apiClient.GetSvgByIdAsync(w.SvgImageId);
                _rounds.Add(new Round(w.WordId, SvgHtmlWrapper.Wrap(svg), w.WordName, trans?.WordName ?? string.Empty,
                    w.Example!.Trim(), string.IsNullOrWhiteSpace(trans?.Example) ? w.Example.Trim() : trans!.Example!.Trim(), w.WordNumber, w.Level ?? string.Empty));
            }

            IsPlaying = true;
            await StartRoundAsync(0);
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

    private async Task StartRoundAsync(int index)
    {
        var round = _rounds[index];
        _target = round.Phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

        var tiles = _target.Select((t, i) => new PhraseTile { Id = i, Text = t }).ToList();
        // Mixed up, and never already in the right order.
        List<PhraseTile> mixed;
        var tries = 0;
        do { mixed = tiles.OrderBy(_ => Random.Shared.Next()).ToList(); tries++; }
        while (tries < 10 && mixed.Select(t => t.Text).SequenceEqual(_target));

        Word = round.Word;
        WordTranslation = round.WordTranslation;
        WordNumber = round.Number;
        WordLevel = round.Level;
        SvgHtml = round.SvgHtml;
        Prompt = round.Prompt;
        Progress = (double)index / _rounds.Count;
        StepText = $"{index + 1} / {_rounds.Count}";

        Bank.Clear();
        foreach (var t in mixed) Bank.Add(t);
        _placed = [];
        _verdict = string.Empty;
        IsInteractive = true;
        RebuildSlots();
        await SpeakAsync(round.Word);
    }

    private void RebuildSlots()
    {
        Slots.Clear();
        for (int i = 0; i < _target.Count; i++)
        {
            var tile = i < _placed.Count ? _placed[i] : null;
            var state = tile is null ? "empty" : _verdict switch
            {
                "right" => "right",
                "wrong" => tile.Text == _target[i] ? "right" : "wrong",
                _ => "filled"
            };
            Slots.Add(new PhraseSlot { Index = i, Tile = tile, State = state });
        }
        CanCheck = _target.Count > 0 && _placed.Count == _target.Count && IsInteractive;
    }

    partial void OnIsInteractiveChanged(bool value) => CanCheck = value && _target.Count > 0 && _placed.Count == _target.Count;

    // A tap on a word below: it goes to the first free cell.
    [RelayCommand]
    private void Append(PhraseTile? tile)
    {
        if (!IsInteractive || tile is null || tile.IsPlaced) return;
        tile.IsPlaced = true;
        _placed.Add(tile);
        _verdict = string.Empty;
        RebuildSlots();
    }

    // A tap on a word in a cell: it goes back below, the words after it move one cell to the left.
    [RelayCommand]
    private void ReturnToBank(PhraseSlot? slot)
    {
        if (!IsInteractive || slot is null || slot.Index >= _placed.Count) return;
        _placed[slot.Index].IsPlaced = false;
        _placed.RemoveAt(slot.Index);
        _verdict = string.Empty;
        RebuildSlots();
    }

    [RelayCommand]
    private async Task Check()
    {
        if (!IsInteractive || !CanCheck) return;
        IsInteractive = false;
        var ok = _placed.Select(t => t.Text).SequenceEqual(_target);
        _verdict = ok ? "right" : "wrong";
        RebuildSlots();

        if (ok)
        {
            Coach.Cheer();
            await SpeakAsync(_rounds[_stepIndex].Phrase);
            await Task.Delay(1800);

            _stepIndex++;
            if (_stepIndex >= _rounds.Count)
            {
                Progress = 1;
                IsGameWon = true;
                IsPlaying = false;
                await _apiClient.EnsureWordsExistAsync(_rounds.Select(r => r.WordId).ToList());
                LivesService.Apply(await _apiClient.MarkGameWonAsync(SubjectId, "Phrase"));      // the server gives the LP (LpReward is only what is shown)
                _topicDone = await _apiClient.GetBookIfTopicDoneAsync(SubjectId) is not null;      // all three games won: "Continue" goes to the snake
            }
            else
            {
                await StartRoundAsync(_stepIndex);
            }
        }
        else
        {
            Coach.Oops();
            bool canPlay = await LivesService.TakeDamageAsync();
            await Task.Delay(500);
            if (!canPlay) { IsGameOver = true; IsPlaying = false; return; }
            IsInteractive = true;          // the red / green marks stay until the next move
            RebuildSlots();
        }
    }

    [RelayCommand]
    private async Task SpeakWord() => await SpeakAsync(Word);

    private async Task SpeakAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith(Jumbo.Shared.SpeechLanguages.Prefix(UserLanguagePrefs.StudyLangId), StringComparison.OrdinalIgnoreCase));
            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Locale = locale, Pitch = 1.0f, Volume = 1.0f }, _ttsCts.Token);
        }
        catch (OperationCanceledException) { }
        catch { /* the voice is a bonus: never break the game */ }
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
    private async Task GoBack()
    {
        _ttsCts?.Cancel();
        await Shell.Current.GoToAsync(IsGameWon && _topicDone ? "../.." : "..");      // "../..": past the topic page to the snake
    }

    [RelayCommand] private void NavigatedFrom() => _ttsCts?.Cancel();
}
