using Jumbo.Services;

namespace Jumbo.Controls;

public partial class TeamPicker : ContentView
{
    public static readonly BindableProperty RegionProperty = BindableProperty.Create(
        nameof(Region), typeof(string), typeof(TeamPicker), string.Empty, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((TeamPicker)b).OnRegionSet((string?)n));

    public static readonly BindableProperty ClassIdProperty = BindableProperty.Create(
        nameof(ClassId), typeof(int?), typeof(TeamPicker), null, BindingMode.TwoWay);

    private readonly List<RegionItem> _regions = UzRegions.Items();
    private bool _syncing;

    /// <summary>Region code ("" = none).</summary>
    public string Region { get => (string)GetValue(RegionProperty); set => SetValue(RegionProperty, value); }

    /// <summary>The team picked from the list (null until chosen). The school comes with the team.</summary>
    public int? ClassId { get => (int?)GetValue(ClassIdProperty); set => SetValue(ClassIdProperty, value); }

    public TeamPicker()
    {
        InitializeComponent();
        RegionPicker.ItemsSource = _regions;
        RegionPicker.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FormPicker.SelectedItem)) _ = OnRegionPickedAsync(); };
        SchoolPicker.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FormPicker.SelectedItem)) _ = OnSchoolPickedAsync(); };
        TeamPickerBox.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(FormPicker.SelectedItem)) OnTeamPicked(); };

        // Region names follow the app language.
        LocalizationManager.Instance.PropertyChanged += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshRegionNames);
    }

    private static LpApiClient Api => IPlatformApplication.Current!.Services.GetRequiredService<LpApiClient>();

    private void RefreshRegionNames()
    {
        var code = (RegionPicker.SelectedItem as RegionItem)?.Code;
        _regions.Clear();
        _regions.AddRange(UzRegions.Items());
        RegionPicker.ItemsSource = null;
        RegionPicker.ItemsSource = _regions;
        if (code is not null) RegionPicker.SelectedItem = _regions.FirstOrDefault(r => r.Code == code);
    }

    private void OnRegionSet(string? code)
    {
        if (_syncing) return;
        var item = _regions.FirstOrDefault(r => r.Code == code);
        if (!ReferenceEquals(RegionPicker.SelectedItem, item)) RegionPicker.SelectedItem = item;
    }

    private async Task OnRegionPickedAsync()
    {
        if (_syncing) return;
        var item = RegionPicker.SelectedItem as RegionItem;

        _syncing = true;
        Region = item?.Code ?? string.Empty;
        _syncing = false;

        ClassId = null;
        SchoolPicker.SelectedItem = null;
        TeamPickerBox.SelectedItem = null;
        TeamPickerBox.ItemsSource = null;
        TeamBlock.IsVisible = false;
        SchoolBlock.IsVisible = item is not null;
        if (item is null) return;

        List<SchoolDto> schools;
        try { schools = await Api.GetDirectorySchoolsAsync(item.Code); }
        catch { schools = []; }
        SchoolPicker.ItemsSource = schools;
        SchoolPicker.IsVisible = schools.Count > 0;
        NoSchools.IsVisible = schools.Count == 0;
    }

    private async Task OnSchoolPickedAsync()
    {
        ClassId = null;
        TeamPickerBox.SelectedItem = null;
        var school = SchoolPicker.SelectedItem as SchoolDto;
        TeamBlock.IsVisible = school is not null;
        if (school is null) return;

        List<TeamOptionDto> teams;
        try { teams = await Api.GetDirectoryTeamsAsync(school.SchoolId); }
        catch { teams = []; }
        TeamPickerBox.ItemsSource = teams;
        TeamPickerBox.IsVisible = teams.Count > 0;
        NoTeams.IsVisible = teams.Count == 0;
        ConfirmHint.IsVisible = teams.Count > 0;
    }

    private void OnTeamPicked() => ClassId = (TeamPickerBox.SelectedItem as TeamOptionDto)?.ClassId;

    /// <summary>Clears every choice (e.g. after the team was sent to the teacher).</summary>
    public void Reset()
    {
        ClassId = null;
        RegionPicker.SelectedItem = null;
    }
}
