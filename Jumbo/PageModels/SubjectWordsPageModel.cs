using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using Jumbo.Services;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(SubjectId), "subjectId")]
public partial class SubjectWordsPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private CancellationTokenSource? _ttsCts;
    private bool _loaded;

    [ObservableProperty] private int _subjectId;
    [ObservableProperty] private string _subjectName = string.Empty;
    [ObservableProperty] private bool _isBusy;

    public ObservableCollection<TopicWordItem> Words { get; } = new();

    public SubjectWordsPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    partial void OnSubjectIdChanged(int value) => _loaded = false;

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;

            var subject = await _apiClient.GetSubjectByIdAsync(SubjectId);
            SubjectName = subject?.SubjectName ?? string.Empty;

            var words = await _apiClient.GetAllWordsBySubjectAndLanguageAsync(SubjectId, UserLanguagePrefs.StudyLangId);
            var ordered = words.OrderBy(w => w.WordNumber).ThenBy(w => w.WordId).ToList();

            await _apiClient.PrefetchWordsAsync(ordered, UserLanguagePrefs.TransLangId);        // all translations at once
            Words.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                var w = ordered[i];
                string translation = string.Empty;
                try
                {
                    var trans = await _apiClient.GetWordBySvgAndLanguageAsync(w.SvgImageId, UserLanguagePrefs.TransLangId);
                    translation = trans?.WordName ?? string.Empty;
                }
                catch (HttpRequestException) { /* one missing translation shouldn't break the list */ }

                Words.Add(new TopicWordItem
                {
                    Number = w.WordNumber > 0 ? w.WordNumber : i + 1,
                    Word = w.WordName,
                    Translate = translation
                });
            }

            _loaded = true;
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

    // Language ids follow the API seed order: 1 = English, 2 = Russian, 3 = Uzbek.
    private static string SpeechLanguagePrefix => UserLanguagePrefs.StudyLangId switch
    {
        2 => "ru",
        3 => "uz",
        _ => "en"
    };

    [RelayCommand]
    private async Task Speak(TopicWordItem? item)
    {
        if (item is null || string.IsNullOrWhiteSpace(item.Word)) return;

        _ttsCts?.Cancel();
        _ttsCts = new CancellationTokenSource();
        try
        {
            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var locale = locales.FirstOrDefault(l => l.Language.StartsWith(SpeechLanguagePrefix, StringComparison.OrdinalIgnoreCase));

            await TextToSpeech.Default.SpeakAsync(item.Word, new SpeechOptions
            {
                Locale = locale,
                Pitch = 1.0f,
                Volume = 1.0f
            }, _ttsCts.Token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (!_loaded)
            await LoadAsync();
    }

    [RelayCommand]
    private void NavigatedFrom() => _ttsCts?.Cancel();
}

public class TopicWordItem
{
    public int Number { get; set; }
    public string Word { get; set; } = string.Empty;
    public string Translate { get; set; } = string.Empty;
}
