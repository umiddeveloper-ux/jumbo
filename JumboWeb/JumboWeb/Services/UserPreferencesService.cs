using Jumbo.ApiClient;

namespace JumboWeb.Services;

/// <summary>
/// Веб-замена статического UserLanguagePrefs из MAUI.
/// Хранит предпочтения пользователя в памяти (Scoped — один экземпляр на соединение).
/// Инициализируется из API при первом обращении.
/// IDs: 1=English, 2=Русский, 3=O'zbekcha
/// </summary>
public class UserPreferencesService
{
    private readonly LpApiClient _apiClient;
    private bool _loaded;

    public int AppLangId   { get; private set; } = 1;
    public int StudyLangId { get; private set; } = 1;
    public int TransLangId { get; private set; } = 2;

    public UserPreferencesService(LpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // Language chosen before signing in (cookie / browser). Signed-in players use their profile language instead.
    public void SetInitialLanguage(string code)
    {
        if (!_loaded) AppLangId = AppLanguages.ToId(code);
    }

    public async Task EnsureLoadedAsync()
    {
        if (_loaded) return;
        var user = await _apiClient.GetCurrentUserAsync();
        if (user is not null)
        {
            AppLangId   = user.AppLanguageId > 0    ? user.AppLanguageId    : 1;
            StudyLangId = user.StudyLanguageId > 0  ? user.StudyLanguageId  : 1;
            TransLangId = user.TranslationLanguageId > 0 ? user.TranslationLanguageId : 2;
        }
        _loaded = true;
    }

    public void Update(int appLangId, int studyLangId, int transLangId)
    {
        AppLangId   = appLangId;
        StudyLangId = studyLangId;
        TransLangId = transLangId;
        _loaded = true;
    }
}
