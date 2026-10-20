using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Shared;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>One of the games that are played over all topics (the same two as on the website's "Games" page).</summary>
public partial class GameKind : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _desc = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

public partial class GameBookChip : ObservableObject
{
    public int Number { get; init; }
    [ObservableProperty] private int _percent;
    [ObservableProperty] private bool _isSelected;
    public bool IsDone => Percent >= 100;
    public string Label => IsDone ? "✓" : Number.ToString();
    partial void OnPercentChanged(int value) { OnPropertyChanged(nameof(IsDone)); OnPropertyChanged(nameof(Label)); }
}

public partial class GameTopicCard : ObservableObject
{
    public int Id { get; init; }
    public int SvgImageId { get; init; }
    public string Name { get; init; } = string.Empty;
    [ObservableProperty] private string _svgHtml = string.Empty;
    [ObservableProperty] private bool _won;
}

/// <summary>
/// The "Games" tab, like the website's /little-polyglot/games: pick a game, then play it over the next topic or pick a book and a topic.
/// </summary>
public partial class GamesPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;

    private readonly Dictionary<int, List<Subject>> _subjectsByBook = new();
    private Dictionary<int, UserSubjectProgress> _progress = new();
    private readonly Dictionary<int, string> _svgByImage = new();
    private List<int> _books = [];
    private int _book = 1;
    private bool _loaded;

    [ObservableProperty] private bool _isBusy;
    // The height of the grid of topic cards: three in a row, 134 + 10 each row (a wrapping layout gave itself far too much height: an empty tail).
    [ObservableProperty] private double _topicsHeight = 144;
    [ObservableProperty] private double _cardWidth = 108;      // set by the page: three cards in a row
    [ObservableProperty] private bool _hasGame;
    [ObservableProperty] private bool _hasNext;
    [ObservableProperty] private string _nextText = string.Empty;
    [ObservableProperty] private ObservableCollection<GameKind> _games = [];
    [ObservableProperty] private ObservableCollection<GameBookChip> _bookChips = [];
    [ObservableProperty] private ObservableCollection<GameTopicCard> _topics = [];

    private GameKind? _game;

    public GamesPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RebuildTexts);
    }

    private static string Key(string? game) => game ?? string.Empty;

    private bool Won(string? game, int subjectId) =>
        _progress.TryGetValue(subjectId, out var p) && (Key(game) == "sequence" ? p.SequenceWon : Key(game) == "steps" && p.StepsWon);

    // The first topic (over all books, in order) where this game is not won yet.
    private Subject? NextTopic(string? game) =>
        _books.SelectMany(b => _subjectsByBook[b]).FirstOrDefault(s => !Won(game, s.SubjectId));

    private int BookPercent(string? game, int book)
    {
        var list = _subjectsByBook.TryGetValue(book, out var l) ? l : [];
        return list.Count == 0 ? 0 : list.Count(s => Won(game, s.SubjectId)) * 100 / list.Count;
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = !_loaded;
            _books = (await _apiClient.GetBookNumbersAsync()).OrderBy(b => b).ToList();
            _subjectsByBook.Clear();
            foreach (var b in _books)
                _subjectsByBook[b] = (await _apiClient.GetSubjectsByBookAsync(b)).OrderBy(s => s.SequenceNumber).ToList();
            _progress = (await _apiClient.GetUserSubjectProgressAsync()).ToDictionary(p => p.SubjectId);

            if (!_loaded)
            {
                // the book of the first topic that is not fully won, else the last book (the same as the website)
                var firstOpen = _books.SelectMany(b => _subjectsByBook[b]).FirstOrDefault(s => !(_progress.TryGetValue(s.SubjectId, out var p) && p.AllWon));
                _book = firstOpen?.BookId ?? _books.LastOrDefault(1);
                Games = new ObservableCollection<GameKind>
                {
                    new() { Key = "sequence", Icon = "🔡" },
                    new() { Key = "steps", Icon = "📊" },
                };
            }
            _loaded = true;
            RebuildTexts();
            await RefreshListAsync();
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private void RebuildTexts()
    {
        foreach (var g in Games)
        {
            g.Title = LocalizationManager.Get(g.Key == "sequence" ? "Game_SequenceTitle" : "Game_StepsTitle");
            g.Desc = LocalizationManager.Get(g.Key == "sequence" ? "Game_SequenceDesc" : "Game_StepsDesc");
        }
        UpdateNext();
    }

    private void UpdateNext()
    {
        var next = _game is null ? null : NextTopic(_game.Key);
        HasNext = next is not null;
        NextText = next is null ? string.Empty : "▶ " + LocalizationManager.Format("Games_Next", next.SubjectName);
    }

    private async Task RefreshListAsync()
    {
        HasGame = _game is not null;
        UpdateNext();
        BookChips = new ObservableCollection<GameBookChip>(_books.Select(b => new GameBookChip { Number = b, Percent = BookPercent(_game?.Key, b), IsSelected = b == _book }));

        var subs = _subjectsByBook.TryGetValue(_book, out var l) ? l : [];
        TopicsHeight = Math.Max(1, (int)Math.Ceiling(subs.Count / 3.0)) * 144;
        Topics = new ObservableCollection<GameTopicCard>(subs.Select(s => new GameTopicCard
        {
            Id = s.SubjectId, SvgImageId = s.SvgImageId, Name = s.SubjectName, Won = Won(_game?.Key, s.SubjectId),
            SvgHtml = _svgByImage.TryGetValue(s.SvgImageId, out var svg) ? svg : string.Empty,
        }));

        // the pictures come after the list (each is a separate request)
        foreach (var card in Topics.ToList())
        {
            if (card.SvgHtml.Length > 0 || card.SvgImageId <= 0) continue;
            try
            {
                var raw = await _apiClient.GetSvgByIdAsync(card.SvgImageId);
                if (string.IsNullOrEmpty(raw)) continue;
                var html = SvgHtmlWrapper.Wrap(raw);
                _svgByImage[card.SvgImageId] = html;
                card.SvgHtml = html;
            }
            catch { /* a picture is decoration */ }
        }
    }

    [RelayCommand]
    private async Task SelectGame(GameKind? game)
    {
        if (game is null) return;
        bool on = !game.IsSelected;
        foreach (var g in Games) g.IsSelected = false;
        game.IsSelected = on;
        _game = on ? game : null;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task SelectBook(GameBookChip? chip)
    {
        if (chip is null) return;
        _book = chip.Number;
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task Play(GameTopicCard? card)
    {
        if (card is null || _game is null) return;
        await PlayAsync(card.Id);
    }

    [RelayCommand]
    private async Task PlayNext()
    {
        if (_game is null || NextTopic(_game.Key) is not { } next) return;
        await PlayAsync(next.SubjectId);
    }

    private Task PlayAsync(int subjectId) =>
        Shell.Current.GoToAsync($"{(_game!.Key == "sequence" ? "SequenceGamePage" : "StepsGamePage")}?subjectId={subjectId}");

    [RelayCommand]
    private async Task Appearing()
    {
        if (!UserSession.IsRegistered) return;
        await LoadAsync();
    }
}
