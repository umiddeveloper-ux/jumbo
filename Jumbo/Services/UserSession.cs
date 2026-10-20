namespace Jumbo.Services;

public static class UserSession
{
    public static int UserId
    {
        get => Preferences.Default.Get("user_id", 0);
        set => Preferences.Default.Set("user_id", value);
    }

    public static string? AccessToken
    {
        get => Preferences.Default.Get("access_token", (string?)null);
        set => Preferences.Default.Set("access_token", value);
    }

    public static bool IsRegistered => !string.IsNullOrEmpty(AccessToken);
}
