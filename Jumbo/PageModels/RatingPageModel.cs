using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Controls;
using Jumbo.Services;
using Jumbo.Shared.Dtos;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

/// <summary>One line of the list: the place, the name (or "Hidden player"), the team and the points of the period.</summary>
public class RatingRow
{
    public string PlaceText { get; init; } = string.Empty;
    public bool IsMedal { get; init; }
    public string Initial { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Team { get; init; } = string.Empty;
    public bool HasTeam => Team.Length > 0;
    public string PointsText { get; init; } = string.Empty;
    public bool IsMe { get; init; }
}

/// <summary>
/// The rating page of the app (the same list as the website's /rating): the period (week / all the time) and the scope (team, school,
/// region, the whole country). Opened from the card at the end of the home page; "back" returns to the home page.
/// </summary>
public partial class RatingPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private RatingDto? _data;
    private bool _started;

    [ObservableProperty] private string _scope = "all";
    [ObservableProperty] private string _period = "week";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _meText = string.Empty;
    [ObservableProperty] private string _meSubText = string.Empty;
    [ObservableProperty] private string _emptyText = string.Empty;
    [ObservableProperty] private bool _showEmpty;
    [ObservableProperty] private bool _showMissing;
    [ObservableProperty] private bool _showList;

    public ObservableCollection<RatingRow> Rows { get; } = [];
    public IList<ChipItem> Periods { get; private set; } = [];
    public IList<ChipItem> Scopes { get; private set; } = [];

    public RatingPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
        BuildChips();
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(() => { BuildChips(); Rebuild(); });
    }

    private static string T(string key) => LocalizationManager.Get(key);

    private void BuildChips()
    {
        Periods = [new ChipItem("week", T("Rating_Week")), new ChipItem("all", T("Rating_AllTime"))];
        Scopes = [new ChipItem("team", T("Rating_ScopeTeam")), new ChipItem("school", T("Rating_ScopeSchool")),
                  new ChipItem("region", T("Rating_ScopeRegion")), new ChipItem("all", T("Rating_ScopeAll"))];
        OnPropertyChanged(nameof(Periods));
        OnPropertyChanged(nameof(Scopes));
    }

    partial void OnScopeChanged(string value) { if (_started) _ = LoadAsync(); }
    partial void OnPeriodChanged(string value) { if (_started) _ = LoadAsync(); }

    [RelayCommand]
    private async Task Appearing()
    {
        if (_started) return;
        // Start from the most personal scope the learner filled in (the same order as the website).
        try
        {
            var me = await _apiClient.GetCurrentUserAsync();
            Scope = !string.IsNullOrWhiteSpace(me?.Team) ? "team"
                  : !string.IsNullOrWhiteSpace(me?.School) ? "school"
                  : !string.IsNullOrWhiteSpace(me?.Region) ? "region" : "all";
        }
        catch { Scope = "all"; }
        _started = true;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Refresh()
    {
        await LoadAsync();
        IsRefreshing = false;
    }

    [RelayCommand]
    private async Task OpenProfile() => await Shell.Current.GoToAsync("//settings");

    private async Task LoadAsync()
    {
        IsBusy = true;
        try { _data = await _apiClient.GetRatingAsync(Scope, Period); }
        catch (Exception ex) { _data = null; _errorHandler.HandleError(ex); }
        finally { IsBusy = false; }
        Rebuild();
    }

    private void Rebuild()
    {
        Rows.Clear();
        ShowMissing = ShowEmpty = ShowList = false;
        MeText = MeSubText = string.Empty;
        if (!_started) return;
        if (_data is null || _data.Missing)
        {
            ShowMissing = true;
            EmptyText = T(Scope switch { "team" => "Rating_MissingTeam", "school" => "Rating_MissingSchool", _ => "Rating_MissingRegion" });
            return;
        }
        if (_data.Me is { } me)
        {
            MeText = LocalizationManager.Format("Rating_MyPlace", me.Rank);
            MeSubText = LocalizationManager.Format("Rating_Points", me.Words);
        }
        else MeText = T("Rating_NoPlace");

        if (_data.Entries.Count == 0)
        {
            ShowEmpty = true;
            EmptyText = T("Rating_Empty");
            return;
        }
        foreach (var e in _data.Entries)
        {
            var name = string.IsNullOrWhiteSpace(e.Name) ? T("Rating_Hidden") : e.Name!;
            Rows.Add(new RatingRow
            {
                PlaceText = e.Rank switch { 1 => "🥇", 2 => "🥈", 3 => "🥉", _ => e.Rank.ToString() },
                IsMedal = e.Rank is >= 1 and <= 3,
                Initial = string.IsNullOrWhiteSpace(e.Name) ? "?" : e.Name![..1].ToUpperInvariant(),
                Name = e.IsMe ? name + " · " + T("Rating_You") : name,
                Team = e.Team ?? string.Empty,
                PointsText = LocalizationManager.Format("Rating_Points", e.Words),
                IsMe = e.IsMe
            });
        }
        ShowList = true;
    }
}
