using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(Letter), "letter")]
public partial class LetterSelectionPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;

    [ObservableProperty] private string _letter = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasPreviewWords;

    public ObservableCollection<LetterWordPreview> PreviewWords { get; } = [];

    public LetterSelectionPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    partial void OnLetterChanged(string value)
    {
        if (!string.IsNullOrEmpty(value))
            _ = LoadPreviewWordsAsync();
    }

    private async Task LoadPreviewWordsAsync()
    {
        try
        {
            IsBusy = true;
            PreviewWords.Clear();

            int langId = UserLanguagePrefs.StudyLangId;
            var words = await _apiClient.GetWordsByLetterAsync(Letter, langId, 6);

            foreach (var word in words)
            {
                var svg = await _apiClient.GetSvgByIdAsync(word.SvgImageId);
                PreviewWords.Add(new LetterWordPreview
                {
                    WordName = word.WordName,
                    SvgHtml = SvgHtmlWrapper.Wrap(svg)
                });
            }

            HasPreviewWords = PreviewWords.Count > 0;
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
    private async Task SelectTest() =>
        await Shell.Current.GoToAsync($"LetterTestPage?letter={Uri.EscapeDataString(Letter)}");

    [RelayCommand]
    private async Task SelectRecall() =>
        await Shell.Current.GoToAsync($"LetterRecallPage?letter={Uri.EscapeDataString(Letter)}");

    [RelayCommand]
    private async Task GoBack() => await Shell.Current.GoToAsync("..");

    [RelayCommand] private void NavigatedTo() { }
    [RelayCommand] private void NavigatedFrom() { }
}

public class LetterWordPreview
{
    public string WordName { get; set; } = string.Empty;
    public string SvgHtml { get; set; } = string.Empty;
}
