namespace Jumbo.Shared;

/// <summary>
/// Turkish ("tr") is a language to study only: the app has no Turkish texts and the words of the Turkish books are never shown as a
/// translation, so the lists of the app language and of the translation language leave it out (the list of the study languages does not).
/// </summary>
public static class LanguageRoles
{
    public static bool IsStudyOnly(string? shortName) => string.Equals(shortName, "tr", StringComparison.OrdinalIgnoreCase);
}
