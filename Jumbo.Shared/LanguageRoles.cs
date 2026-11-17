namespace Jumbo.Shared;

/// <summary>
/// Every language in the Languages table can now be the app's interface language, the translation language, or the one being
/// studied - Turkish included, since the site and the app both have a Turkish interface (AppResources.tr.resx). Kept as a hook
/// in case a future language is ever added as study-only before its interface text exists.
/// </summary>
public static class LanguageRoles
{
    public static bool IsStudyOnly(string? shortName) => false;
}
