using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Mvvm.Input;
using Jumbo.ApiClient;
using System.Collections.ObjectModel;

namespace Jumbo.PageModels;

[QueryProperty(nameof(SubjectId), "subjectId")]
public partial class SubjectDetailPageModel : ObservableObject
{
    private readonly LpApiClient _apiClient;
    private readonly ModalErrorHandler _errorHandler;
    private bool _isNavigatedTo;
    private bool _dataLoaded;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _subjectId;

    [ObservableProperty]
    private string _subjectName = string.Empty;

    // The number of the topic in the three-part ring (one part per won game), as on the website's topic page.
    [ObservableProperty]
    private int _topicNumber;

    [ObservableProperty]
    private int _parts;

    // The video of the topic (a file on our server); a topic that has none shows no player.
    [ObservableProperty]
    private bool _hasVideo;

    [ObservableProperty]
    private MediaSource? _videoSource;

    private static readonly HttpClient Probe = new() { Timeout = TimeSpan.FromSeconds(8) };

    [ObservableProperty]
    private ObservableCollection<LessonItemViewModel> _lessons = [];

    public SubjectDetailPageModel(LpApiClient apiClient, ModalErrorHandler errorHandler)
    {
        _apiClient = apiClient;
        _errorHandler = errorHandler;
    }

    private async Task LoadSubjectDetailsAsync()
    {
        try
        {
            // The progress the learner had here last time is shown at once; the network only brings the news (a page opened for the second
            // time does not make a child wait). The topic comes from the cache of the client.
            bool seenBefore;
            Shared.Entities.UserSubjectProgress? last;
            lock (LastProgress) seenBefore = LastProgress.TryGetValue(ProgressKey, out last);
            IsBusy = Lessons.Count == 0 && !seenBefore;

            var subjectTask = _apiClient.GetSubjectByIdAsync(SubjectId);
            var progressTask = _apiClient.GetUserSubjectProgressAsync(SubjectId);        // at the same time
            var subject = await subjectTask;
            if (subject != null)
            {
                SubjectName = subject.SubjectName;
                TopicNumber = subject.SequenceNumber;
                _ = PrepareVideoAsync(subject.BookId, subject.SequenceNumber);     // does not hold the page back
            }
            if (seenBefore) { ShowLessons(last); IsBusy = false; }

            if (subject != null && await RedirectIfLockedAsync(subject)) return;

            var progress = await progressTask;
            lock (LastProgress) LastProgress[ProgressKey] = progress;
            ShowLessons(progress);
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

    // what each learner (the key has the login token) had in each topic the last time: shown at once when the topic is opened again
    private static readonly Dictionary<string, Shared.Entities.UserSubjectProgress?> LastProgress = new();
    private string ProgressKey => (UserSession.AccessToken ?? string.Empty) + ":" + SubjectId;

    private void ShowLessons(Shared.Entities.UserSubjectProgress? progress)
    {
        Parts = progress?.PartsDone ?? 0;
        Lessons = new ObservableCollection<LessonItemViewModel>(
        [
            new() { Type = "Words",        Name = LocalizationManager.Get("Game_WordsTitle"), Desc = LocalizationManager.Get("Game_WordsDesc"), SvgIcon = "lesson_words.png", IsPassed = false },
            new() { Type = "FindMe",       Name = LocalizationManager.Get("Game_FindMeTitle"), Desc = LocalizationManager.Get("Game_FindMeDesc"), SvgIcon = "lesson_findme.png", IsPassed = progress?.FindMeWon  ?? false },
            new() { Type = "ListenAndFind",Name = LocalizationManager.Get("Game_ListenTitle"), Desc = LocalizationManager.Get("Game_ListenDesc"), SvgIcon = "lesson_listen.png", IsPassed = progress?.ListenWon   ?? false },
            new() { Type = "Phrase",       Name = LocalizationManager.Get("Game_PhraseTitle"), Desc = LocalizationManager.Get("Game_PhraseDesc"), SvgIcon = "lesson_phrase.png", IsPassed = progress?.PhraseWon   ?? false }
        ]);
    }

    // A topic opens only after the topic before it is passed: otherwise the learner is told so and sent back (or to the topic to pass).
    private async Task<bool> RedirectIfLockedAsync(Shared.Entities.Subject subject)
    {
        try
        {
            var progress = (await _apiClient.GetUserSubjectProgressAsync()).ToDictionary(p => p.SubjectId);
            bool Done(Shared.Entities.Subject t) => progress.TryGetValue(t.SubjectId, out var p) && p.PartsDone >= Shared.Entities.UserSubjectProgress.Parts;
            var book = (await _apiClient.GetSubjectsByBookAsync(subject.BookId)).OrderBy(t => t.SequenceNumber).ToList();
            var cur = book.FirstOrDefault(t => !Done(t));
            if (cur is null || Done(subject)) return false;
            if (book.FindIndex(t => t.SubjectId == subject.SubjectId) <= book.FindIndex(t => t.SubjectId == cur.SubjectId)) return false;

            IsBusy = false;
            bool go = await Shell.Current.DisplayAlertAsync(
                "🔒 " + LocalizationManager.Get("Topic_LockedTitle"),
                LocalizationManager.Format("Topic_LockedText", cur.SubjectName),
                LocalizationManager.Get("Topic_Go"),
                LocalizationManager.Get("Topic_Understood"));
            await Shell.Current.GoToAsync("..");
            if (go) await Shell.Current.GoToAsync($"SubjectDetailPage?subjectId={cur.SubjectId}");
            return true;
        }
        catch (Exception) { return false; }      // the check is a guard: never break the page
    }

    // The file is /videos/{lang}/book-NN/MM.mp4 on the server: the player is shown only if the file is there.
    private static readonly Dictionary<string, bool> VideoKnown = new();

    private async Task PrepareVideoAsync(int bookId, int sequence)
    {
        // /videos/{language the learner studies}/book-NN/MM.mp4
        var url = ApiConfig.VideoBaseUrl.TrimEnd('/') + Jumbo.Shared.TopicVideoPaths.Url(Jumbo.Shared.TopicVideoPaths.LanguageCode(UserLanguagePrefs.StudyLangId), bookId, sequence);
        bool known;
        lock (VideoKnown) known = VideoKnown.TryGetValue(url, out var has) && has;
        if (known)
        {
            if (!HasVideo) { VideoSource = MediaSource.FromUri(url); HasVideo = true; }      // the same page again: the player stays as it is
            return;
        }
        lock (VideoKnown) if (VideoKnown.ContainsKey(url)) { HasVideo = false; VideoSource = null; return; }      // asked before: no video

        HasVideo = false;
        VideoSource = null;
        try
        {
            using var head = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await Probe.SendAsync(head, HttpCompletionOption.ResponseHeadersRead);
            bool ok = response.IsSuccessStatusCode;
            lock (VideoKnown) VideoKnown[url] = ok;
            if (!ok) return;
            VideoSource = MediaSource.FromUri(url);
            HasVideo = true;
        }
        catch { /* no network: no player now, asked again next time */ }
    }

    [RelayCommand]
    private async Task OpenLesson(LessonItemViewModel lesson)
    {
        if (lesson == null) return;

        string route = lesson.Type switch
        {
            "Words"        => "SubjectWordsPage",
            "FindMe"       => "FindMeGamePage",
            "Sequence"     => "SequenceGamePage",
            "ListenAndFind"=> "ListenGamePage",
            "Steps"        => "StepsGamePage",
            "Phrase"       => "PhraseGamePage",
            _              => string.Empty
        };

        if (!string.IsNullOrEmpty(route))
            await Shell.Current.GoToAsync($"{route}?subjectId={SubjectId}");
    }

    partial void OnSubjectIdChanged(int value)
    {
        if (value > 0)
            _dataLoaded = false;
    }

    [RelayCommand]
    private void NavigatedTo() => _isNavigatedTo = true;

    [RelayCommand]
    private void NavigatedFrom() => _isNavigatedTo = false;

    [RelayCommand]
    private async Task Appearing()
    {
        if (!_dataLoaded)
            _dataLoaded = true;
        await LoadSubjectDetailsAsync();
    }
}

public partial class LessonItemViewModel : ObservableObject
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Desc { get; set; } = string.Empty;
    public string SvgIcon { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isPassed;
}
