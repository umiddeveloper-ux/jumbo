using Jumbo.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Jumbo.Controls;

/// <summary>"🌐 RU" pill: tap to pick the app language (Русский / English / O'zbekcha). Texts update at once.</summary>
public class LanguageSwitcher : Border
{
    private readonly Label _label = new()
    {
        FontFamily = "NunitoExtraBold",
        FontSize = 14,
        VerticalOptions = LayoutOptions.Center,
        HorizontalOptions = LayoutOptions.Center
    };

    public LanguageSwitcher()
    {
        StrokeShape = new RoundRectangle { CornerRadius = 12 };
        StrokeThickness = 2;
        Padding = new Thickness(10, 0);
        HeightRequest = 36;
        HorizontalOptions = LayoutOptions.End;
        this.SetAppThemeColor(BackgroundColorProperty, (Color)Application.Current!.Resources["SurfaceLight"], (Color)Application.Current!.Resources["SurfaceDark"]);
        Content = _label;
        GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await PickAsync()) });

        Update();
        Loaded += (_, _) => { LocalizationManager.Instance.PropertyChanged += OnChanged; ApplyStroke(); };
        Unloaded += (_, _) => LocalizationManager.Instance.PropertyChanged -= OnChanged;
        if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => ApplyStroke();
    }

    private void OnChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => MainThread.BeginInvokeOnMainThread(Update);

    private void ApplyStroke()
    {
        bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
        Stroke = new SolidColorBrush((Color)Application.Current!.Resources[dark ? "BorderDark" : "BorderLight"]);
        _label.TextColor = (Color)Application.Current!.Resources["PrimaryText"];
    }

    private void Update() => _label.Text = "🌐 " + LocalizationService.CurrentCode.ToUpperInvariant();

    private async Task PickAsync()
    {
        var names = LocalizationService.Languages.Select(l => l.Name).ToArray();
        string? picked = await Shell.Current.DisplayActionSheetAsync("🌐", "✕", null, names);
        var lang = LocalizationService.Languages.FirstOrDefault(l => l.Name == picked);
        if (lang.Code is null) return;
        LocalizationService.SetLanguage(lang.Code);
    }
}
