namespace Jumbo.Services;

/// <summary>
/// The colours of the app that change while it runs: the brand orange and the colour of the level of the book (books 1-3 A1 red, 4-6 A2 yellow,
/// 7-10 B1 green, 11+ B2 violet; the front of the printed card), light and dark. They are written into Application.Resources (keys Primary,
/// PrimaryDark, PrimaryEdge, PrimarySoft, PrimaryEdgeSoft, OnPrimary, PrimaryText and the frame Brand, BrandDark, OnBrand); the XAML takes them with
/// {DynamicResource ...}, so everything changes at once. The controls that draw themselves (the snake, the rings) read <see cref="Primary"/> and
/// the others below and redraw when <see cref="Changed"/> is raised.
/// </summary>
public static class ThemePalette
{
    // primary, primary dark, edge of the button, soft background, soft edge, text on the primary, the primary as a text on a light page
    private sealed record Set(string P, string Dark, string Edge, string Soft, string EdgeSoft, string On, string Text);

    private static readonly Dictionary<string, (Set Light, Set Dark)> Sets = new()
    {
        [""] = (new("#F28C0F", "#DB7A00", "#C46F00", "#FFF0DC", "#FFD08A", "#1B1B1F", "#B45F00"),
                new("#FFA733", "#FFA733", "#C46F00", "#3A2A12", "#7A4C0A", "#17171A", "#FFA733")),
        ["a1"] = (new("#E5393B", "#C92A2D", "#B02A2C", "#FDE8E8", "#F5A5A6", "#FFFFFF", "#C92A2D"),
                  new("#FF6B6D", "#FF6B6D", "#B02A2C", "#3D1D1E", "#8A3A3C", "#17171A", "#FF6B6D")),
        ["a2"] = (new("#FFC21A", "#F0B000", "#C99400", "#FFF5D1", "#FFE28A", "#3B2A00", "#8A6200"),
                  new("#FFD54A", "#FFD54A", "#C99400", "#3A3213", "#8A7020", "#17171A", "#FFD54A")),
        ["b1"] = (new("#2F9E44", "#2A8C3D", "#237A33", "#E4F6E5", "#9AD9A3", "#FFFFFF", "#237A33"),
                  new("#5CD16A", "#5CD16A", "#2F9E44", "#183322", "#2F6B3A", "#17171A", "#5CD16A")),
        ["b2"] = (new("#7C4DFF", "#6A3DE8", "#5B34C7", "#EDE7FF", "#C8B6FF", "#FFFFFF", "#5B34C7"),
                  new("#A98BFF", "#A98BFF", "#7A5AD8", "#2A2245", "#5B47A0", "#17171A", "#A98BFF")),
    };

    public static event EventHandler? Changed;

    /// <summary>"" (the brand), "a1", "a2", "b1" or "b2".</summary>
    public static string Level { get; private set; } = string.Empty;

    /// <summary>The level of the book that the learner looked at last (the games and the pages of a topic take it).</summary>
    public static string BookLevel { get; set; } = string.Empty;

    public static bool Dark => Application.Current?.RequestedTheme == AppTheme.Dark;

    public static string LevelOfBook(int book) => book switch { <= 0 => string.Empty, <= 3 => "a1", <= 6 => "a2", <= 10 => "b1", _ => "b2" };

    private static Set Current => Sets.TryGetValue(Level, out var s) ? (Dark ? s.Dark : s.Light) : (Dark ? Sets[""].Dark : Sets[""].Light);

    // colours for the controls that draw themselves
    public static Color Primary => Color.FromArgb(Current.P);
    public static Color PrimaryEdge => Color.FromArgb(Current.Edge);
    public static Color PrimarySoft => Color.FromArgb(Current.Soft);
    public static Color OnPrimary => Color.FromArgb(Current.On);
    public static Color PrimaryText => Color.FromArgb(Current.Text);
    public static Color Brand => Color.FromArgb(Dark ? "#E8890C" : "#F28C0F");
    public static Color OnBrand => Color.FromArgb(Dark ? "#17171A" : "#1B1B1F");

    /// <summary>Starts to follow the theme of the system / the choice of the learner.</summary>
    public static void Init()
    {
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => Apply();
        Apply();
    }

    public static void SetLevel(string level)
    {
        level ??= string.Empty;
        if (Level == level) return;
        Level = level;
        Apply();
    }

    /// <summary>Writes the colours into the resources of the app (all the {DynamicResource} users change) and tells the drawing controls.</summary>
    public static void Apply()
    {
        void Write()
        {
            if (Application.Current is not { } app) return;
            var s = Current;
            var r = app.Resources;
            r["Primary"] = Color.FromArgb(s.P);
            r["PrimaryDark"] = Color.FromArgb(s.Dark);
            r["PrimaryEdge"] = Color.FromArgb(s.Edge);
            r["PrimarySoft"] = Color.FromArgb(s.Soft);
            r["PrimaryEdgeSoft"] = Color.FromArgb(s.EdgeSoft);
            r["OnPrimary"] = Color.FromArgb(s.On);
            r["PrimaryText"] = Color.FromArgb(s.Text);
            // the frame of the app (the top bar): always the orange of the brand
            var b = Dark ? Sets[""].Dark : Sets[""].Light;
            r["Brand"] = Color.FromArgb(Dark ? "#E8890C" : b.P);
            r["BrandDark"] = Color.FromArgb(Dark ? "#C46F00" : b.Dark);
            r["OnBrand"] = Color.FromArgb(b.On);
#if ANDROID
            // the bar of the phone above the title bar has the same orange (it took a fixed colour from the Android theme before)
            try
            {
                CommunityToolkit.Maui.Core.Platform.StatusBar.SetColor(Brand);
                CommunityToolkit.Maui.Core.Platform.StatusBar.SetStyle(CommunityToolkit.Maui.Core.StatusBarStyle.DarkContent);
            }
            catch { /* no window yet: the next change of the palette sets it */ }
#endif
            Changed?.Invoke(null, EventArgs.Empty);
        }
        if (MainThread.IsMainThread) Write(); else MainThread.BeginInvokeOnMainThread(Write);
    }
}
