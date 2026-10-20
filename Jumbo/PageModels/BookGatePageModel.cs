using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Shared;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>One of the three steps of the check.</summary>
public partial class GateStepRow : ObservableObject
{
    public int Number { get; init; }
    public string Label { get; init; } = string.Empty;       // "Step 1"
    public string Name { get; init; } = string.Empty;        // the game
    public string State { get; init; } = "future";           // done | current | future
    public string Marker => State == "done" ? "✓" : Number.ToString();
    public bool IsDone => State == "done";
    public bool IsCurrent => State == "current";
    public string ButtonText { get; init; } = string.Empty;
    public string DoneText { get; init; } = string.Empty;
}

/// <summary>
/// The check that opens a book: three games over the words of the previous book. Passing all three opens the book for good and
/// lands the learner in it. (Same rule as the website's "Opening book N" page.)
/// </summary>
[QueryProperty(nameof(Book), "book")]
public partial class BookGatePageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly BookGateService _gate;
    private readonly ModalErrorHandler _errorHandler;
    private bool _finishing;

    [ObservableProperty] private int _book;
    [ObservableProperty] private bool _isOpened;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _subtitle = string.Empty;
    [ObservableProperty] private string _openedText = string.Empty;
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private ObservableCollection<GateStepRow> _steps = [];

    public BookGatePageModel(LpApiClient apiClient, BookGateService gate, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _gate = gate;
        _errorHandler = errorHandler;
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(BuildTexts);
    }

    partial void OnBookChanged(int value) => BuildTexts();

    private void BuildTexts()
    {
        if (Book < 2) return;
        Title = LocalizationManager.Format("Gate_Title", Book);
        Subtitle = LocalizationManager.Format("Gate_Sub", Book, Book - 1);
        OpenedText = LocalizationManager.Format("Gate_Opened", Book);

        var next = Enumerable.Range(1, BookGateService.Steps).FirstOrDefault(s => !_gate.IsPassed(Book, s), BookGateService.Steps + 1);
        var rows = new List<GateStepRow>();
        for (int s = 1; s <= BookGateService.Steps; s++)
        {
            var done = _gate.IsPassed(Book, s);
            rows.Add(new GateStepRow
            {
                Number = s,
                Label = LocalizationManager.Format("Gate_Step", s),
                Name = LocalizationManager.Get(s switch { 1 => "Game_FindMeTitle", 2 => "Game_ListenTitle", _ => "Game_SequenceTitle" }),
                State = done ? "done" : s == next ? "current" : "future",
                ButtonText = LocalizationManager.Get(_gate.PassedCount(Book) == 0 ? "Gate_Start" : "Gate_Next") + " →",
                DoneText = LocalizationManager.Get("Gate_Passed")
            });
        }
        Steps = new ObservableCollection<GateStepRow>(rows);
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (!UserSession.IsRegistered || Book < 2 || _finishing) return;
        try
        {
            var open = await _apiClient.GetUnlockedBooksAsync();
            if (Book > Course.BookCount || !open.Contains(Book - 1))
            {
                await Shell.Current.GoToAsync("..");     // the book before it is still closed: nothing to check here
                return;
            }
            if (open.Contains(Book)) { await OpenBookAsync(); return; }

            BuildTexts();
            if (_gate.PassedCount(Book) >= BookGateService.Steps) await FinishAsync();
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    private async Task FinishAsync()
    {
        _finishing = true;
        try
        {
            if (await _apiClient.UnlockBookAsync(Book))
            {
                _gate.Reset(Book);
                IsOpened = true;
                await Task.Delay(1500);
                await OpenBookAsync();
            }
            else
            {
                _gate.Reset(Book);
                ErrorText = LocalizationManager.Get("Gate_Error");
                HasError = true;
                BuildTexts();
            }
        }
        finally { _finishing = false; }
    }

    private async Task OpenBookAsync()
    {
        _gate.PendingOpenBook = Book;
        await Shell.Current.GoToAsync("//main");
    }

    [RelayCommand]
    private async Task StartStep(GateStepRow? row)
    {
        if (row is null || !row.IsCurrent) return;
        await Shell.Current.GoToAsync(BookGateService.StepRoute(row.Number, Book));
    }
}
