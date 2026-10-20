using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;

namespace Jumbo.PageModels;

public partial class AlphabetPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private bool _isNavigatedTo;
    private bool _dataLoaded;

    [ObservableProperty]
    private List<LetterItem> _alphabet = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    public AlphabetPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    private async Task LoadAlphabetAsync()
    {
        try
        {
            IsBusy = true;
            Alphabet = await _apiClient.GetAlphabetProgressAsync(UserLanguagePrefs.StudyLangId);
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

    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            await LoadAlphabetAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task LetterSelected(LetterItem letter)
    {
        if (letter is null) return;
        await Shell.Current.GoToAsync($"LetterSelectionPage?letter={Uri.EscapeDataString(letter.Letter)}");
    }

    [RelayCommand]
    private async Task NavigatedTo()
    {
        _isNavigatedTo = true;

        if (!_dataLoaded)
        {
            _dataLoaded = true;
            await LoadAlphabetAsync();
        }
    }

    [RelayCommand]
    private void NavigatedFrom() => _isNavigatedTo = false;

    [RelayCommand]
    private async Task Appearing()
    {
        if (!_dataLoaded)
        {
            _dataLoaded = true;
            await LoadAlphabetAsync();
        }
        else if (!_isNavigatedTo)
        {
            await Refresh();
        }
    }
}
