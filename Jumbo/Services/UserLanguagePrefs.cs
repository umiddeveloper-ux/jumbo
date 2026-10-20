namespace Jumbo.Services;

/// <summary>
/// Sync access to user language IDs stored in Preferences.
/// Populated at registration and on settings save — no DB round-trip needed in game models.
/// IDs match the Language table: 1=English, 2=Русский, 3=O'zbekcha
/// </summary>
public static class UserLanguagePrefs
{
    // Language 1 (🌐 button): controls AppResources / UI strings
    public static int AppLangId   => Preferences.Default.Get("app_lang_id",   2); // default: Russian
    // Language 2: the language being learned (words shown as questions)
    public static int StudyLangId => Preferences.Default.Get("study_lang_id", 1); // default: English
    // Language 3: translation shown as hints/answers
    public static int TransLangId => Preferences.Default.Get("trans_lang_id", 2); // default: Russian

    public static void Save(int appLangId, int studyLangId, int transLangId)
    {
        Preferences.Default.Set("app_lang_id",   appLangId);
        Preferences.Default.Set("study_lang_id", studyLangId);
        Preferences.Default.Set("trans_lang_id", transLangId);
    }
}
