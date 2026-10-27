using Jumbo.Controls;
using Jumbo.Services;
using Jumbo.Shared.Dtos;

namespace Jumbo.Pages;

/// <summary>
/// The window of an ad (see AdService): the picture if there is one, the title, the text (simple html, cleaned by the server) and the
/// button "Close". Shown as a modal page; <see cref="Result"/> is done when it is closed (by the button or the Back key).
/// </summary>
public class AdPage : ContentPage
{
    private readonly TaskCompletionSource _closed = new();
    public Task Result => _closed.Task;

    public AdPage(AdForUserDto ad)
    {
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F3F5F8"), Color.FromArgb("#0C0C0E"));
        Shell.SetNavBarIsVisible(this, false);

        var card = new VerticalStackLayout { Spacing = 14, Padding = new Thickness(20), MaximumWidthRequest = 520, VerticalOptions = LayoutOptions.Center };

        if (ImageOf(ad.ImageDataUrl) is { } image)
            card.Children.Add(new Border
            {
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                StrokeThickness = 0,
                Padding = 0,
                Content = new Image { Source = image, Aspect = Aspect.AspectFill, HeightRequest = 190 }
            });

        card.Children.Add(new Label
        {
            Text = ad.Title, FontFamily = "NunitoBlack", FontSize = 24, HorizontalTextAlignment = TextAlignment.Center
        });

        var text = new Label { Text = ad.Html, TextType = TextType.Html, FontSize = 16, LineBreakMode = LineBreakMode.WordWrap };
        text.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#1B1B1F"), Color.FromArgb("#F2F2F4"));
        card.Children.Add(text);

        var close = new ChunkyButton { Text = LocalizationManager.Get("Ad_Close"), Kind = "success" };
        close.Clicked += (_, _) => _closed.TrySetResult();
        card.Children.Add(close);

        Content = new ScrollView { Content = card, VerticalOptions = LayoutOptions.Center };
    }

    private static ImageSource? ImageOf(string? dataUrl)
    {
        if (string.IsNullOrEmpty(dataUrl)) return null;
        try
        {
            int comma = dataUrl.IndexOf(',');
            if (comma < 0) return null;
            var bytes = Convert.FromBase64String(dataUrl[(comma + 1)..]);
            return ImageSource.FromStream(() => new MemoryStream(bytes));
        }
        catch { return null; }
    }

    protected override bool OnBackButtonPressed()
    {
        _closed.TrySetResult();
        return true;
    }
}
