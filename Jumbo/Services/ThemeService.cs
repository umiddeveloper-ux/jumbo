namespace Jumbo.Services;

/// <summary>
/// App theme: "system" (follow the phone), "light" or "dark". Stored on the device; the website's header toggle
/// switches light/dark, the settings page offers all three.
/// </summary>
public static class ThemeService
{
    private const string Key = "theme_mode";

    public static event Action? Changed;

    // JUMBO_THEME (light|dark|system) sets the default for screenshots / manual checks.
    public static string Mode => Preferences.Default.Get(Key, Environment.GetEnvironmentVariable("JUMBO_THEME") ?? "light");

    public static bool IsDark => (Application.Current?.RequestedTheme ?? AppTheme.Light) == AppTheme.Dark;

    public static void Apply()
    {
        var theme = Mode switch { "dark" => AppTheme.Dark, "system" => AppTheme.Unspecified, _ => AppTheme.Light };
        if (Application.Current is { } app) app.UserAppTheme = theme;
        Changed?.Invoke();
    }

    public static void SetMode(string mode)
    {
        Preferences.Default.Set(Key, mode);
        Apply();
    }

    /// <summary>The header button: flips between light and dark.</summary>
    public static void Toggle() => SetMode(IsDark ? "light" : "dark");
}
