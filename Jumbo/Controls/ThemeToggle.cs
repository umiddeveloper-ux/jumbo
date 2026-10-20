using Jumbo.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>Sun / moon button: switches the app between the light and the dark theme (same as the website's header button).</summary>
public class ThemeToggle : Border
{
    private readonly Label _icon = new() { FontSize = 18, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };

    public ThemeToggle()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 12 };
        StrokeThickness = 2;
        Padding = 0;
        WidthRequest = 36;
        HeightRequest = 36;
        HorizontalOptions = LayoutOptions.End;
        this.SetAppThemeColor(BackgroundColorProperty, (Color)Application.Current!.Resources["SurfaceLight"], (Color)Application.Current!.Resources["SurfaceDark"]);
        Content = _icon;
        GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(ThemeService.Toggle) });

        Refresh();
        ThemeService.Changed += () => MainThread.BeginInvokeOnMainThread(Refresh);
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Refresh);
    }

    private void Refresh()
    {
        _icon.Text = ThemeService.IsDark ? "☀️" : "🌙";
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        Stroke = new SolidColorBrush((Color)Application.Current!.Resources[dark ? "BorderDark" : "BorderLight"]);
    }
}
