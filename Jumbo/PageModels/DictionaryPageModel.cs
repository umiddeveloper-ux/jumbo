using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using Jumbo.Utilities;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

public partial class DictionaryPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;

    private int _pageNumber = 0;
    private const int _pageSize = 15;
    private bool _isNavigatedTo;
    private bool _allDataLoaded;
    private bool _suppressSearchReload;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<WordItem> Words { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearchActive))]
    private string _searchText = string.Empty;

    public bool IsSearchActive => !string.IsNullOrWhiteSpace(SearchText);

    public DictionaryPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    async partial void OnSearchTextChanged(string value)
    {
        // Игнорируем сброс SearchText, который делает сам Refresh() — там перезагрузка уже запущена явно.
        if (_suppressSearchReload) return;

        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;

        try
        {
            // Debounce: ждём паузу в наборе текста, чтобы не слать запрос на каждое нажатие клавиши.
            await Task.Delay(300, cts.Token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        _allDataLoaded = false;
        await LoadDataAsync(isRefresh: true, cts.Token);
    }

    private async Task LoadDataAsync(bool isRefresh, CancellationToken token = default)
    {
        if (_allDataLoaded && !isRefresh) return;

        try
        {
            IsBusy = true;

            int pageToLoad = isRefresh ? 0 : _pageNumber;

            int studyLangId = UserLanguagePrefs.StudyLangId;
            int transLangId  = UserLanguagePrefs.TransLangId;

            var rawWords = await _apiClient.GetPagedWordsAsync(
                SearchText, pageToLoad, _pageSize, studyLangId);

            // Более новый поисковый запрос уже стартовал — не затираем его результаты устаревшими.
            if (token.IsCancellationRequested) return;

            var newItems = new List<WordItem>();
            if (rawWords != null)
            {
                await _apiClient.PrefetchWordsAsync(rawWords.Where(w => w.SvgImageId > 0), transLangId, wholeTopics: false);        // a page of words at once
                foreach (var word in rawWords)
                {
                    if (token.IsCancellationRequested) return;

                    string translate = word.WordName;
                    string svgHtml   = string.Empty;

                    try
                    {
                        if (word.SvgImageId > 0)
                        {
                            var transWord = await _apiClient.GetWordBySvgAndLanguageAsync(
                                word.SvgImageId, transLangId);
                            translate = transWord?.WordName ?? word.WordName;

                            var rawSvg = await _apiClient.GetSvgByIdAsync(word.SvgImageId);
                            if (!string.IsNullOrWhiteSpace(rawSvg))
                                svgHtml = SvgHtmlWrapper.Wrap(rawSvg);
                        }
                    }
                    catch { /* одно слово не ломает весь список */ }

                    newItems.Add(new WordItem
                    {
                        Number    = word.WordNumber,
                        Word      = word.WordName,
                        Translate = translate,
                        SvgHtml   = svgHtml,
                        Example   = word.Example?.Trim() ?? string.Empty
                    });
                }
            }

            if (token.IsCancellationRequested) return;

            if (isRefresh)
                Words.Clear();
            foreach (var item in newItems)
                Words.Add(item);

            _pageNumber = pageToLoad + 1;
            if (rawWords == null || rawWords.Count < _pageSize)
                _allDataLoaded = true;
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
            _searchCts?.Cancel();

            _suppressSearchReload = true;
            SearchText = string.Empty;
            _suppressSearchReload = false;

            _allDataLoaded = false;
            await LoadDataAsync(isRefresh: true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    private CancellationTokenSource? _ttsCts;

    // The speaker next to a word: it is read aloud in English.
    [RelayCommand]
    private async Task Speak(WordItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Word)) return;

        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith(Jumbo.Shared.SpeechLanguages.Prefix(UserLanguagePrefs.StudyLangId), StringComparison.OrdinalIgnoreCase));
            await TextToSpeech.Default.SpeakAsync(item.Word, new SpeechOptions { Locale = locale, Pitch = 1.0f, Volume = 1.0f }, _ttsCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    [RelayCommand]
    private async Task LoadMoreItems()
    {
        if (IsBusy || IsRefreshing || _allDataLoaded) return;
        await LoadDataAsync(isRefresh: false);
    }

    [RelayCommand]
    private void NavigatedTo() => _isNavigatedTo = true;

    [RelayCommand]
    private void NavigatedFrom()
    {
        _isNavigatedTo = false;
        _ttsCts?.Cancel();
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (Words.Count == 0)
        {
            IsBusy = true;
            await Task.Delay(150);
            await LoadDataAsync(isRefresh: true);
            IsBusy = false;
        }
        else if (!_isNavigatedTo)
        {
            await Refresh();
        }
    }

}
