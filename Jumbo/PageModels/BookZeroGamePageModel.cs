using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.Controls;
using Jumbo.Services;
using Jumbo.Shared;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Jumbo.PageModels;

/// <summary>A tile of the games of Book 0: a letter or a number.</summary>
public partial class ZeroGameTile : ObservableObject
{
    public string Key { get; init; } = string.Empty;       // "LA" / "N7"
    public string Big { get; init; } = string.Empty;       // "A" / "7"
    public string Small { get; init; } = string.Empty;     // "a" (letters only)
    public string Spoken { get; init; } = string.Empty;    // what the voice says
    public bool HasSmall => Small.Length > 0;

    /// <summary>none | ok | bad | used</summary>
    [ObservableProperty]
    private string _state = "none";
}

/// <summary>
/// The games of Book 0 (the alphabet and the numbers). Nothing here is counted: no lives, no LP, a mistake costs only a star.
///   listen: the voice says a letter or a number, the learner taps it among six (10 rounds);
///   train:  a train of seven letters / numbers in order with two wagons missing, the learner puts them in (6 rounds).
/// The best result of each game and the letters / numbers that were found without a mistake are kept on the device.
/// </summary>
[QueryProperty(nameof(Mode), "mode")]
[QueryProperty(nameof(Kind), "kind")]
public partial class BookZeroGamePageModel : ObservableObject
{
    private const int ListenRounds = 10, TrainRounds = 6, WagonCount = 7;

    private readonly Random _rnd = new();
    private CancellationTokenSource? _speech;
    private List<ZeroGameTile> _pool = [];
    private List<ZeroGameTile> _targets = [];
    private ZeroGameTile? _target;
    private List<ZeroGameTile> _line = [];                 // the seven of the train
    private List<int> _gaps = [];
    private int _filled;
    private int _round, _mistakes;
    private bool _roundClean = true;
    private bool _locked;
    private bool _started;

    // the train: text and state of the locomotive and of the seven wagons; the page draws it when this fires
    private readonly List<TrainWagon> _train = [];
    public event Action<IReadOnlyList<TrainWagon>>? TrainChanged;
    private void PublishTrain() => TrainChanged?.Invoke(_train.ToList());
    private void SetWagon(int lineIndex, string text, string state)
    {
        _train[lineIndex + 1] = new TrainWagon(text, state);       // (the first of them is the locomotive)
        PublishTrain();
    }

    /// <summary>Jumbo the coach: cheers right answers, comforts after wrong ones.</summary>
    public GameCoach Coach { get; } = new();

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(IsTrain), nameof(IsListen), nameof(Title))] private string _mode = "listen";
    [ObservableProperty] private string _kind = "letters";

    public bool IsTrain => Mode == "train";
    public bool IsListen => !IsTrain;
    public string Title => LocalizationManager.Get(IsTrain ? "ZG_TrainTitle" : "ZG_ListenTitle");
    private int Rounds => IsTrain ? TrainRounds : ListenRounds;

    [ObservableProperty] private bool _isFinished;
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _progressText = string.Empty;
    [ObservableProperty] private double _progressValue;
    [ObservableProperty] private string _hintText = string.Empty;
    [ObservableProperty] private string _starsText = string.Empty;
    [ObservableProperty] private string _mistakesText = string.Empty;
    [ObservableProperty] private ObservableCollection<ZeroGameTile> _options = [];

    private int Stars => _mistakes == 0 ? 3 : _mistakes <= 2 ? 2 : 1;

    // ── lifecycle ───────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task Appearing()
    {
        if (_started) return;
        _started = true;
        await Start();
    }

    [RelayCommand]
    private void NavigatedFrom() => StopSpeech();

    [RelayCommand]
    private async Task Start()
    {
        if (Mode != "train") Mode = "listen";
        if (Kind != "numbers") Kind = "letters";
        _pool = Kind == "numbers"
            ? Enumerable.Range(0, BookZeroData.MaxNumber + 1).Select(n => new ZeroGameTile { Key = "N" + n, Big = n.ToString(), Spoken = BookZeroData.NumberWord(n) }).ToList()
            : BookZeroData.Letters.Select(l => new ZeroGameTile { Key = "L" + l.Letter, Big = l.Letter, Small = l.Letter.ToLowerInvariant(), Spoken = l.Spoken }).ToList();

        _round = 0;
        _mistakes = 0;
        IsFinished = false;
        IsPlaying = true;
        _targets = _pool.OrderBy(_ => _rnd.Next()).Take(ListenRounds).ToList();
        HintText = LocalizationManager.Get(IsTrain ? "ZG_TrainHint" : "ZG_ListenHint");
        await NewRound();
    }

    private async Task NewRound()
    {
        _roundClean = true;
        _locked = false;
        _filled = 0;
        Coach.Reset();
        UpdateProgress();

        // fresh tiles every round (a tile carries its state)
        ZeroGameTile Fresh(ZeroGameTile t) => new() { Key = t.Key, Big = t.Big, Small = t.Small, Spoken = t.Spoken };

        if (IsTrain)
        {
            int start = _rnd.Next(0, _pool.Count - WagonCount + 1);
            _line = _pool.Skip(start).Take(WagonCount).ToList();
            int g1 = _rnd.Next(1, WagonCount - 1), g2;
            do { g2 = _rnd.Next(1, WagonCount - 1); } while (Math.Abs(g2 - g1) < 2);      // not the ends, not next to each other
            _gaps = new List<int> { g1, g2 }.OrderBy(x => x).ToList();

            var missing = _gaps.Select(i => _line[i]);
            var others = _pool.Where(p => _line.All(w => w.Key != p.Key)).OrderBy(_ => _rnd.Next()).Take(2);
            Options = new ObservableCollection<ZeroGameTile>(missing.Concat(others).OrderBy(_ => _rnd.Next()).Select(Fresh));

            _train.Clear();
            _train.Add(new TrainWagon("🚂", "loco"));
            for (int i = 0; i < _line.Count; i++)
                _train.Add(new TrainWagon(_gaps.Contains(i) ? "?" : _line[i].Big, _gaps.Contains(i) ? (i == _gaps[0] ? "next" : "gap") : "fixed"));
            PublishTrain();
        }
        else
        {
            _target = _targets[_round];
            var others = _pool.Where(p => p.Key != _target.Key).OrderBy(_ => _rnd.Next()).Take(5);
            Options = new ObservableCollection<ZeroGameTile>(others.Append(_target).OrderBy(_ => _rnd.Next()).Select(Fresh));
            await SpeakTarget();
        }
    }

    private void UpdateProgress()
    {
        ProgressText = $"{Math.Min(_round + 1, Rounds)} / {Rounds}";
        ProgressValue = _round / (double)Rounds;
    }

    /// <summary>A tile was tapped: in a listening round it is the answer, in the train it is the wagon to put in.</summary>
    [RelayCommand]
    private Task Tap(ZeroGameTile? tile) => IsTrain ? Place(tile) : Pick(tile);

    // ── listen and tap ──────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SpeakTarget()
    {
        if (_target is not null) await Say(_target);
    }

    private async Task Pick(ZeroGameTile? tile)
    {
        if (tile is null || _locked || _target is null || tile.State != "none") return;
        if (tile.Key == _target.Key)
        {
            _locked = true;
            tile.State = "ok";
            Coach.Cheer();
            if (_roundClean) Remember(tile.Key);
            await Task.Delay(900);
            await NextRound();
        }
        else
        {
            tile.State = "bad";
            _mistakes++;
            _roundClean = false;
            Coach.Oops();
            await Say(tile);        // what was tapped, so the learner hears the difference
        }
    }

    // ── the missing wagon: the tile has to fit the first empty place of the train ──
    private async Task Place(ZeroGameTile? tile)
    {
        if (tile is null || _locked || tile.State == "used") return;
        var gap = _filled < _gaps.Count ? _gaps[_filled] : -1;
        if (gap >= 0 && _line[gap].Key == tile.Key)
        {
            tile.State = "used";
            SetWagon(gap, _line[gap].Big, "put");
            _filled++;
            if (_filled < _gaps.Count) SetWagon(_gaps[_filled], "?", "next");
            _ = Say(tile);
            if (_filled >= _gaps.Count)
            {
                _locked = true;
                Coach.Cheer();
                if (_roundClean) foreach (var g in _gaps) Remember(_line[g].Key);
                await Task.Delay(1100);
                await NextRound();
            }
        }
        else
        {
            tile.State = "bad";
            _mistakes++;
            _roundClean = false;
            Coach.Oops();
            await Task.Delay(500);
            if (tile.State == "bad") tile.State = "none";
        }
    }

    private async Task NextRound()
    {
        _round++;
        if (_round >= Rounds)
        {
            IsPlaying = false;
            IsFinished = true;
            StarsText = string.Concat(Enumerable.Range(1, 3).Select(i => i <= Stars ? "★" : "☆"));
            MistakesText = LocalizationManager.Format("ZG_Mistakes", _mistakes);
            Coach.Reset();
            SaveBest();
            return;
        }
        await NewRound();
    }

    [RelayCommand]
    private async Task Back()
    {
        StopSpeech();
        await Shell.Current.GoToAsync("..");
    }

    // ── what the device remembers ───────────────────────────────────────────
    private void SaveBest()
    {
        var key = $"bz-best-{Mode}-{Kind}";
        if (Stars > Preferences.Default.Get(key, 0)) Preferences.Default.Set(key, Stars);
    }

    private static void Remember(string itemKey)
    {
        try
        {
            var json = Preferences.Default.Get("bz-known", string.Empty);
            var known = string.IsNullOrEmpty(json) ? new Dictionary<string, int>() : JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
            known[itemKey] = known.GetValueOrDefault(itemKey) + 1;
            Preferences.Default.Set("bz-known", JsonSerializer.Serialize(known));
        }
        catch { /* the memory is a bonus */ }
    }

    // ── the voice ───────────────────────────────────────────────────────────
    private void StopSpeech()
    {
        _speech?.Cancel();
        _speech = null;
    }

    private async Task Say(ZeroGameTile tile)
    {
        StopSpeech();
        _speech = new CancellationTokenSource();
        var token = _speech.Token;
        var text = tile.Spoken;
        try
        {
            if (await LetterAudio.PlayAsync(tile.Key, token)) return;      // a letter has a recording
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Locale = locale, Pitch = 1.0f, Volume = 1.0f }, token);
        }
        catch (OperationCanceledException) { }
        catch { /* the voice is a bonus: never break the game */ }
    }
}
