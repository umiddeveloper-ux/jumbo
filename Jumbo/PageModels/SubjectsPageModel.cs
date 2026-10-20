using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

// Автоматически связывает параметр "bookNumber" из навигации со свойством BookNumber
[QueryProperty(nameof(BookNumber), "bookNumber")]
public partial class SubjectsPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private bool _isNavigatedTo;
    private bool _dataLoaded;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isRefreshing;

    // Свойство для приема номера книги из Dashboard
    [ObservableProperty]
    private int _bookNumber;

    [ObservableProperty]
    private ObservableCollection<SubjectItemViewModel> _subjects = [];

    public SubjectsPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    private async Task LoadSubjectsAsync()
    {
        try
        {
            IsBusy = true;

            var dbSubjects = await _apiClient.GetSubjectsByBookAsync(BookNumber);

            var enLang = await _apiClient.GetLanguageByShortNameAsync("en");
            int enId = enLang?.LanguageId ?? 0;

            var allProgress = await _apiClient.GetUserSubjectProgressAsync();
            var progressLookup = allProgress.ToDictionary(p => p.SubjectId);

            Subjects.Clear();
            foreach (var subject in dbSubjects)
            {
                string rawSvg = string.Empty;

                if (subject.SvgImageId > 0)
                {
                    // Нормальный путь: у субъекта есть своя иконка
                    rawSvg = await _apiClient.GetSvgByIdAsync(subject.SvgImageId);
                }

                // Fallback для старых данных (SvgImageId == 0): берём SVG первого слова
                if (string.IsNullOrEmpty(rawSvg))
                {
                    var firstWord = await _apiClient.GetWordsBySubjectAndLanguageAsync(subject.SubjectId, enId, 1);
                    if (firstWord.Count > 0)
                        rawSvg = await _apiClient.GetSvgByIdAsync(firstWord[0].SvgImageId);
                }

                bool allWon = progressLookup.TryGetValue(subject.SubjectId, out var prog) && prog.AllWon;

                Subjects.Add(new SubjectItemViewModel
                {
                    Id = subject.SubjectId,
                    Name = subject.SubjectName,
                    SvgHtml = SvgHtmlWrapper.Wrap(rawSvg),
                    IsPassed = allWon
                });
            }
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
    private async Task SelectSubject(SubjectItemViewModel selectedSubject)
    {
        if (selectedSubject == null) return;

        await Shell.Current.GoToAsync($"SubjectDetailPage?subjectId={selectedSubject.Id}");
    }

    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            IsRefreshing = true;
            await LoadSubjectsAsync();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    partial void OnBookNumberChanged(int value)
    {
        if (value > 0)
            _dataLoaded = false;
    }

    [RelayCommand]
    private async Task NavigatedTo()
    {
        _isNavigatedTo = true;

        if (!_dataLoaded)
        {
            _dataLoaded = true;
            await LoadSubjectsAsync();
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
            await LoadSubjectsAsync();
        }
        else if (!_isNavigatedTo)
        {
            await Refresh();
        }
    }
}

// Вспомогательная вью-модель для отображения элементов списка в UI
public partial class SubjectItemViewModel : ObservableObject
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SvgHtml { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isPassed;
}