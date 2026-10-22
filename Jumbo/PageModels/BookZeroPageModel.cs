using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.Services;
using Jumbo.Shared;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>A tile of Book 0: a letter or a number.</summary>
public partial class ZeroTile : ObservableObject
{
    public string Big { get; init; } = string.Empty;        // "A" / "7"
    public string Small { get; init; } = string.Empty;      // "a" / "seven"
    public string Translation { get; set; } = string.Empty; // the number in the learner's language
    public string Spoken { get; init; } = string.Empty;     // what the voice says
    public bool HasTranslation => !string.IsNullOrEmpty(Translation);
    public bool IsVowel { get; init; }                      // a vowel letter is shown in colour
    public string Key { get; init; } = string.Empty;        // "LA" / "N7": the key under which the games remember it
    public string DigitColor { get; init; } = "#1CB0F6";    // the colour of a number's digit

    [ObservableProperty] private bool _isActive;

    /// <summary>Found three times without a mistake in the games: a green dot on the tile.</summary>
    [ObservableProperty] private bool _isKnown;
}

/// <summary>
/// Book 0: the alphabet and the numbers up to 20 with the sound. A reference that is always open: no topics, no progress, no check,
/// nothing here counts for the level or for opening the other books.
/// </summary>
public partial class BookZeroPageModel : ObservableObject
{
    private static readonly string[] Hues = ["#1CB0F6", "#58CC02", "#FF9600", "#FF6B9D", "#A06CF0"];

    private CancellationTokenSource? _cts;

    public ObservableCollection<ZeroTile> Letters { get; } = [];
    public ObservableCollection<ZeroTile> Numbers { get; } = [];

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(LettersButtonText))] private bool _playingLetters;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(NumbersButtonText))] private bool _playingNumbers;

    public string LettersButtonText => (PlayingLetters ? "■ " + LocalizationManager.Get("Zero_Stop") : "▶ " + LocalizationManager.Get("Zero_PlayAll"));
    public string NumbersButtonText => (PlayingNumbers ? "■ " + LocalizationManager.Get("Zero_Stop") : "▶ " + LocalizationManager.Get("Zero_PlayAll"));

    public BookZeroPageModel()
    {
        foreach (var (letter, spoken) in BookZeroData.Letters)
            Letters.Add(new ZeroTile { Key = "L" + letter, Big = letter, Small = letter.ToLowerInvariant(), Spoken = spoken, IsVowel = BookZeroData.IsVowel(letter) });
        for (int n = 0; n <= BookZeroData.MaxNumber; n++)
            Numbers.Add(new ZeroTile { Key = "N" + n, Big = n.ToString(), Small = BookZeroData.NumberWord(n), Spoken = BookZeroData.NumberWord(n), DigitColor = Hues[n % Hues.Length] });

        // the words of the buttons follow the language switch
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            OnPropertyChanged(nameof(LettersButtonText));
            OnPropertyChanged(nameof(NumbersButtonText));
        });
    }

    private void FillTranslations()
    {
        for (int n = 0; n < Numbers.Count; n++)
            Numbers[n].Translation = BookZeroData.NumberTranslation(n, UserLanguagePrefs.TransLangId) ?? string.Empty;
        OnPropertyChanged(nameof(Numbers));
    }

    // the best results of the games (stars), as the device remembers them
    [ObservableProperty] private string _listenLettersBest = string.Empty;
    [ObservableProperty] private string _listenNumbersBest = string.Empty;
    [ObservableProperty] private string _trainLettersBest = string.Empty;
    [ObservableProperty] private string _trainNumbersBest = string.Empty;

    private static string BestOf(string mode, string kind)
    {
        int stars = Preferences.Default.Get($"bz-best-{mode}-{kind}", 0);
        return stars <= 0 ? string.Empty : string.Concat(Enumerable.Range(1, 3).Select(i => i <= stars ? "★" : "☆"));
    }

    private void LoadMemory()
    {
        ListenLettersBest = BestOf("listen", "letters");
        ListenNumbersBest = BestOf("listen", "numbers");
        TrainLettersBest = BestOf("train", "letters");
        TrainNumbersBest = BestOf("train", "numbers");
        try
        {
            var json = Preferences.Default.Get("bz-known", string.Empty);
            var known = string.IsNullOrEmpty(json) ? new Dictionary<string, int>() : System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
            foreach (var t in Letters.Concat(Numbers)) t.IsKnown = known.GetValueOrDefault(t.Key) >= 3;
        }
        catch { /* the memory is a bonus */ }
    }

    /// <summary>Opens a game: "listen|letters", "train|numbers" ...</summary>
    [RelayCommand]
    private async Task OpenGame(string? which)
    {
        if (which is null) return;
        var p = which.Split('|');
        if (p.Length == 2) await Shell.Current.GoToAsync($"BookZeroGamePage?mode={p[0]}&kind={p[1]}");
    }

    [RelayCommand]
    private void Appearing()
    {
        LoadMemory();
        FillTranslations();
        // the numbers are read again with the translation of the learner's language
        var copy = Numbers.ToList();
        Numbers.Clear();
        foreach (var t in copy) Numbers.Add(t);
    }

    [RelayCommand]
    private async Task Say(ZeroTile? tile)
    {
        if (tile is null) return;
        Stop();
        Light(tile);
        await SpeakAsync(tile, CancellationToken.None);
        tile.IsActive = false;
    }

    // "Listen to all": the letters (or the numbers) one after another; the tile that is being read lights up. The same button stops it.
    [RelayCommand]
    private async Task PlayLetters() => await PlayAllAsync(Letters, letters: true);

    [RelayCommand]
    private async Task PlayNumbers() => await PlayAllAsync(Numbers, letters: false);

    private async Task PlayAllAsync(ObservableCollection<ZeroTile> tiles, bool letters)
    {
        bool wasPlaying = letters ? PlayingLetters : PlayingNumbers;
        Stop();
        if (wasPlaying) return;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        if (letters) PlayingLetters = true; else PlayingNumbers = true;
        try
        {
            foreach (var tile in tiles.ToList())
            {
                if (token.IsCancellationRequested) break;
                Light(tile);
                await SpeakAsync(tile, token);
                tile.IsActive = false;
                if (!token.IsCancellationRequested) await Task.Delay(250, token);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (_cts?.Token == token) { PlayingLetters = PlayingNumbers = false; ClearLights(); }
        }
    }

    private void Light(ZeroTile tile)
    {
        ClearLights();
        tile.IsActive = true;
    }

    private void ClearLights()
    {
        foreach (var t in Letters) t.IsActive = false;
        foreach (var t in Numbers) t.IsActive = false;
    }

    private void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        PlayingLetters = PlayingNumbers = false;
        ClearLights();
    }

    [RelayCommand]
    private void NavigatedFrom() => Stop();

    // a letter is played from its recording; the voice says it only when there is none (and always says a number)
    private static async Task SpeakAsync(ZeroTile tile, CancellationToken token)
    {
        if (await LetterAudio.PlayAsync(tile.Key, token)) return;
        var text = tile.Spoken;
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase));
            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Locale = locale, Pitch = 1.0f, Volume = 1.0f }, token);
        }
        catch (OperationCanceledException) { throw; }
        catch { /* the voice is a bonus: never break the page */ }
    }
}
